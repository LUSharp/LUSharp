using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor;
using LUSharpTranspiler.AST.SourceConstructor.Builders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transpiler.Builder
{
    public class ClassBuilder
    {
        /// <summary>
        /// Name of the class
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Public global class members (Player.Damage(self,plr))
        /// </summary>
        public List<MemberDeclarationSyntax> StaticMembers = new();
        /// <summary>
        /// Members that are accessed by instance. (plr.health)
        /// </summary>
        public List<MemberDeclarationSyntax> InstanceMembers = new();

        /// <summary>
        /// Converts a C# class declaration into a Lua-compatible ClassBuilder.
        /// Processes constructors, properties, static members, and methods.
        /// </summary>
        /// <param name="classDeclaration">The Roslyn syntax node representing the C# class</param>
        /// <returns>A ClassBuilder configured for Lua serialization</returns>
        /// <example>
        /// Input C# class:
        /// <code>
        /// public class Player {
        ///     public string Name { get; set; }
        ///     public int Health { get; set; }
        ///     public static List&lt;string&gt; Inventory = new() { "Sword", "Shield" };
        /// }
        /// </code>
        /// Output Lua class with constructor, getters/setters, and static fields
        /// </example>
        public static ClassBuilder FromCClass(ClassDeclarationSyntax classDeclaration)
        {
            // Initialize the class builder with metadata from C# syntax tree
            ClassBuilder cs = new();
            cs.ClassName = classDeclaration.Identifier.ValueText;
            cs.StaticMembers = [.. classDeclaration.Members.Where(x => Helpers.HasModifier(x, SyntaxKind.StaticKeyword))];
            cs.InstanceMembers = [.. classDeclaration.Members.Where(x => Helpers.HasModifier(x, SyntaxKind.PublicKeyword) && !Helpers.HasModifier(x, SyntaxKind.StaticKeyword))];

            // Create the Lua source constructor builder
            SourceConstructor.ClassBuilder builder = SourceConstructor.ClassBuilder.Create(cs.ClassName);

            // Special handling for Main class (application entry point)
            if (IsMainClass(classDeclaration))
            {
                BuildMainClass(builder);
                Logger.Log(builder.Build());
                return cs;
            }

            // Build all class components in order
            BuildConstructor(builder, cs, classDeclaration);           // Constructor with parameters and field initialization
            BuildPropertyAccessors(builder, cs.InstanceMembers);       // get_PropertyName() and set_PropertyName() methods
            BuildStaticMembers(builder, cs.StaticMembers);             // Static fields and properties
            BuildInstanceMethods(builder, cs.InstanceMembers);         // Instance methods (TODO: body transpilation)

            Logger.Log("\n" + builder.Build());
            return cs;
        }

        #region Main Class Handling

        /// <summary>
        /// Determines if this is the Main class (application entry point).
        /// </summary>
        /// <example>
        /// public class Main { } // Returns true
        /// public class Player { } // Returns false
        /// </example>
        private static bool IsMainClass(ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Identifier.Text == "Main";
        }

        /// <summary>
        /// Builds a special Main class with GameEntry method for Lua initialization.
        /// The Main class serves as the entry point for the Lua application.
        /// </summary>
        /// <example>
        /// C# Input:
        /// <code>
        /// public class Main { }
        /// </code>
        /// Lua Output:
        /// <code>
        /// Main = {}
        /// function Main.GameEntry()
        ///     -- Entry point logic
        /// end
        /// </code>
        /// </example>
        private static void BuildMainClass(SourceConstructor.ClassBuilder builder)
        {
            builder.WithMethod("GameEntry", fn =>
            {
                // Main entry point logic - this is where the Lua script execution begins
            });
        }

        #endregion

        #region Constructor Building

        /// <summary>
        /// Builds the constructor for the Lua class. If no C# constructor exists,
        /// creates a default one from public properties.
        /// </summary>
        /// <example>
        /// With explicit constructor:
        /// <code>
        /// public Player(string name, int health) {
        ///     this.Name = name;
        ///     this.Health = health;
        /// }
        /// </code>
        /// Without constructor (auto-generated):
        /// <code>
        /// public class Player {
        ///     public string Name { get; set; }
        ///     public int Health { get; set; }
        /// }
        /// // Creates: function Player.new(Name, Health)
        /// </code>
        /// </example>
        private static void BuildConstructor(SourceConstructor.ClassBuilder builder, ClassBuilder cs, ClassDeclarationSyntax classDeclaration)
        {
            if (!HasUserDefinedConstructor(classDeclaration))
            {
                BuildDefaultConstructor(builder, cs.InstanceMembers);
            }
            else
            {
                BuildUserDefinedConstructor(builder, cs.InstanceMembers);
            }
        }

        /// <summary>
        /// Checks if the class has a user-defined constructor.
        /// </summary>
        /// <returns>True if a constructor declaration exists</returns>
        private static bool HasUserDefinedConstructor(ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Members.Any(x => x.IsKind(SyntaxKind.ConstructorDeclaration));
        }

        /// <summary>
        /// Creates a default constructor from all public properties.
        /// Useful when the C# class uses auto-properties without explicit constructor.
        /// </summary>
        /// <example>
        /// C# Input:
        /// <code>
        /// public class Player {
        ///     public string Name { get; set; }
        ///     public int Level { get; set; }
        /// }
        /// </code>
        /// Lua Output:
        /// <code>
        /// function Player.new(Name, Level)
        ///     local self = {}
        ///     self.Name = Name
        ///     self.Level = Level
        ///     return self
        /// end
        /// </code>
        /// </example>
        private static void BuildDefaultConstructor(SourceConstructor.ClassBuilder builder, List<MemberDeclarationSyntax> instanceMembers)
        {
            Logger.Log(LUSharp.Logger.LogSeverity.Warning, $"No default constructor found. Creating default.");

            builder.WithConstructor(c =>
            {
                // Add each property as a constructor parameter and initialize it
                foreach (var member in instanceMembers)
                {
                    if (member is PropertyDeclarationSyntax prop)
                    {
                        c.WithParameter(prop.Identifier.Text);
                        c.WithPrivateField(prop.Identifier.Text, prop.Identifier.ValueText);
                    }
                }
            });
        }

        /// <summary>
        /// Processes a user-defined C# constructor and converts it to Lua.
        /// Extracts parameters and field assignments from the constructor body.
        /// </summary>
        /// <example>
        /// C# Input:
        /// <code>
        /// public Player(string name, int health, bool isAlive) {
        ///     this.Name = name;
        ///     this.Health = health;
        ///     this.IsAlive = isAlive;
        /// }
        /// </code>
        /// Lua Output:
        /// <code>
        /// function Player.new(name, health, isAlive)
        ///     local self = {}
        ///     self.Name = name
        ///     self.Health = health
        ///     self.IsAlive = isAlive
        ///     return self
        /// end
        /// </code>
        /// </example>
        private static void BuildUserDefinedConstructor(SourceConstructor.ClassBuilder builder, List<MemberDeclarationSyntax> instanceMembers)
        {
            var constructor = instanceMembers.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            var parameters = constructor?.ParameterList;

            if (parameters == null) return;

            builder.WithConstructor(c =>
            {
                // Add all constructor parameters
                AddConstructorParameters(c, parameters);

                // Process field assignments from constructor body (this.Field = parameter)
                AddConstructorFieldAssignments(c, constructor);
            });
        }

        /// <summary>
        /// Adds all parameters from the C# constructor to the Lua constructor.
        /// </summary>
        /// <example>
        /// public Player(string name, int level, bool active) { }
        /// Creates Lua parameters: name, level, active
        /// </example>
        private static void AddConstructorParameters(ConstructorBuilder c, ParameterListSyntax parameters)
        {
            foreach (var param in parameters.Parameters)
            {
                c.WithParameter(param.Identifier.Text);
            }
        }

        /// <summary>
        /// Processes field assignments in the constructor body and converts them to Lua.
        /// Only processes simple assignments like: this.Property = parameter;
        /// </summary>
        /// <example>
        /// C# constructor body:
        /// <code>
        /// this.Name = name;
        /// this.Health = health;
        /// </code>
        /// Lua output:
        /// <code>
        /// self.Name = name
        /// self.Health = health
        /// </code>
        /// </example>
        private static void AddConstructorFieldAssignments(ConstructorBuilder c, ConstructorDeclarationSyntax? constructor)
        {
            if (constructor?.Body == null) return;

            // Find all assignment expressions in the constructor body
            foreach (var assignment in constructor.Body.Statements.OfType<ExpressionStatementSyntax>())
            {
                if (assignment.Expression is AssignmentExpressionSyntax assignExpr)
                {
                    var left = assignExpr.Left as IdentifierNameSyntax;   // this.PropertyName
                    var right = assignExpr.Right as IdentifierNameSyntax;          // parameterName

                    // Get the property from the class and apply a default value if necessary
                    var parentClass = constructor.Parent as ClassDeclarationSyntax;

                    var property = parentClass?.Members
                        .OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.ValueText == left?.Identifier.ValueText);

                    if(property == null)
                    {
                        Logger.LogException("Failed to find property for constructor assignment: " + left?.Identifier.ValueText);
                        break;
                    }

                    // check for default value
                    var defaultValue = property.Initializer?.Value;
                    if (defaultValue != null) {
                        // handle literal default values
                        if (defaultValue is LiteralExpressionSyntax les)
                        {
                            c.WithPrivateField(left.Identifier.ValueText, right.Identifier.Text, les.Token.Value ?? string.Empty);
                            continue;
                        }
                    }

                    if (left != null && right != null)
                    {
                        c.WithPrivateField(left.Identifier.ValueText, right.Identifier.Text);
                    }
                }
            }
        }

        #endregion

        #region Property Accessors

        /// <summary>
        /// Creates getter and setter methods for all public properties.
        /// Lua doesn't have native property syntax, so we generate explicit methods.
        /// </summary>
        /// <example>
        /// C# Input:
        /// <code>
        /// public string Name { get; set; }
        /// public int Health { get; private set; } // Only getter created
        /// </code>
        /// Lua Output:
        /// <code>
        /// function Player:get_Name()
        ///     return self.Name
        /// end
        /// function Player:set_Name(value)
        ///     self.Name = value
        /// end
        /// function Player:get_Health()
        ///     return self.Health
        /// end
        /// </code>
        /// </example>
        private static void BuildPropertyAccessors(SourceConstructor.ClassBuilder builder, List<MemberDeclarationSyntax> instanceMembers)
        {
            foreach (var member in instanceMembers)
            {
                if (member is PropertyDeclarationSyntax prop)
                {
                    BuildGetter(builder, prop);
                    BuildSetter(builder, prop);
                }
            }
        }

        /// <summary>
        /// Creates a getter method for a property if it has a get accessor.
        /// </summary>
        /// <example>
        /// C# Property: public string Name { get; set; }
        /// Lua Getter:
        /// <code>
        /// function Player:get_Name()
        ///     return self.Name
        /// end
        /// </code>
        /// </example>
        private static void BuildGetter(SourceConstructor.ClassBuilder builder, PropertyDeclarationSyntax prop)
        {
            if (prop.AccessorList?.Accessors.Any(x => x.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
            {
                builder.WithMethod($"get_{prop.Identifier.Text}", method =>
                {
                    method.WithParameter("self")
                        .Return($"self.{prop.Identifier.Text}");
                });
            }
            else
            {
                Logger.Log(LogSeverity.Warning, $"No get accessor found for {prop.Identifier.Text}, skipping creation of getter method");
            }
        }

        /// <summary>
        /// Creates a setter method for a property if it has a set accessor.
        /// </summary>
        /// <example>
        /// C# Property: public int Health { get; set; }
        /// Lua Setter:
        /// <code>
        /// function Player:set_Health(value)
        ///     self.Health = value
        /// end
        /// </code>
        /// </example>
        private static void BuildSetter(SourceConstructor.ClassBuilder builder, PropertyDeclarationSyntax prop)
        {
            if (prop.AccessorList?.Accessors.Any(x => x.IsKind(SyntaxKind.SetAccessorDeclaration)) == true)
            {
                builder.WithMethod($"set_{prop.Identifier.Text}", method =>
                {
                    method.WithParameter("self")
                        .WithParameter("value")
                        .Assign($"self.{prop.Identifier.Text}", "value");
                });
            }
            else
            {
                Logger.Log(LogSeverity.Warning, $"No set accessor found for {prop.Identifier.Text}, skipping creation of setter method");
            }
        }

        #endregion

        #region Static Members

        /// <summary>
        /// Processes all static members (fields and properties) and adds them to the class.
        /// Static members in Lua are stored directly on the class table, not instances.
        /// </summary>
        /// <example>
        /// C# Input:
        /// <code>
        /// public static string GameTitle = "My Game";
        /// public static List&lt;string&gt; Items = new() { "Sword", "Shield" };
        /// public static int MaxPlayers { get; set; } = 10;
        /// </code>
        /// Lua Output:
        /// <code>
        /// Player.GameTitle = "My Game"
        /// Player.Items = {"Sword", "Shield"}
        /// Player.MaxPlayers = 10
        /// </code>
        /// </example>
        private static void BuildStaticMembers(SourceConstructor.ClassBuilder builder, List<MemberDeclarationSyntax> staticMembers)
        {
            foreach (var member in staticMembers)
            {
                if (member is PropertyDeclarationSyntax prop)
                {
                    BuildStaticProperty(builder, prop);
                }
                else if (member is FieldDeclarationSyntax field)
                {
                    BuildStaticField(builder, field);
                }
            }
        }

        /// <summary>
        /// Processes static properties with initializers.
        /// </summary>
        /// <example>
        /// C# Input: public static string Version { get; set; } = "1.0.0";
        /// Lua Output: MyClass.Version = "1.0.0"
        /// </example>
        private static void BuildStaticProperty(SourceConstructor.ClassBuilder builder, PropertyDeclarationSyntax prop)
        {
            if (prop.Initializer?.Value is LiteralExpressionSyntax les)
            {
                builder.WithField(prop.Identifier.Text, les.Token.Value ?? string.Empty);
            }
        }

        /// <summary>
        /// Processes static field declarations with various initializer types.
        /// Supports literals, collections (lists/dictionaries), and nested structures.
        /// </summary>
        /// <example>
        /// Simple literal:
        /// <code>
        /// public static string Title = "Game";
        /// // Lua: MyClass.Title = "Game"
        /// </code>
        /// 
        /// Simple list:
        /// <code>
        /// public static List&lt;int&gt; Scores = new() { 100, 200, 300 };
        /// // Lua: MyClass.Scores = {100, 200, 300}
        /// </code>
        /// 
        /// Dictionary:
        /// <code>
        /// public static Dictionary&lt;string, string&gt; Config = new() {
        ///     { "host", "localhost" },
        ///     { "port", "8080" }
        /// };
        /// // Lua: MyClass.Config = {host = "localhost", port = "8080"}
        /// </code>
        /// 
        /// Nested collections:
        /// <code>
        /// public static List&lt;List&lt;int&gt;&gt; Grid = new() {
        ///     new() { 1, 2, 3 },
        ///     new() { 4, 5, 6 }
        /// };
        /// // Lua: MyClass.Grid = {{1, 2, 3}, {4, 5, 6}}
        /// </code>
        /// </example>
        private static void BuildStaticField(SourceConstructor.ClassBuilder builder, FieldDeclarationSyntax field)
        {
            var firstVariable = field.Declaration.Variables.FirstOrDefault();
            if (firstVariable?.Initializer == null) return;

            switch (firstVariable.Initializer.Value)
            {
                // Simple values: string, int, bool, etc.
                case LiteralExpressionSyntax les:
                    builder.WithField(firstVariable.Identifier.Text, les.Token.Value ?? string.Empty);
                    break;

                // Collections: new List<T>() { ... } or new Dictionary<K,V>() { ... }
                case ImplicitObjectCreationExpressionSyntax implicitObjectCreation:
                    var result = ProcessInitializer(implicitObjectCreation.Initializer, firstVariable.Identifier.Text);
                    if (result != null)
                    {
                        builder.WithField(firstVariable.Identifier.Text, result);
                    }
                    break;

                default:
                    Logger.Log(LogSeverity.Warning, $"Unsupported static field initializer for {firstVariable.Identifier.Text}, skipping.");
                    break;
            }
        }

        #endregion

        #region Instance Methods

        /// <summary>
        /// Processes instance methods and converts them to Lua functions.
        /// TODO: Implement method body transpilation.
        /// </summary>
        /// <example>
        /// C# Input:
        /// <code>
        /// public int CalculateScore(int bonus) {
        ///     return this.BaseScore + bonus;
        /// }
        /// </code>
        /// Lua Output (when implemented):
        /// <code>
        /// function Player:CalculateScore(bonus)
        ///     return self.BaseScore + bonus
        /// end
        /// </code>
        /// </example>
        private static void BuildInstanceMethods(SourceConstructor.ClassBuilder builder, List<MemberDeclarationSyntax> instanceMembers)
        {
            foreach (var member in instanceMembers)
            {
                if (member is MethodDeclarationSyntax method)
                {
                    // TODO: Implement method body transpilation
                    // This requires converting C# expressions, statements, and control flow to Lua
                    // builder.WithMethod(method.Identifier.Text, m =>
                    // {
                    //     // Add parameters
                    //     m.WithParameter("self");
                    //     foreach (var param in method.ParameterList.Parameters)
                    //     {
                    //         m.WithParameter(param.Identifier.Text);
                    //     }
                    //     
                    //     // Transpile method body
                    //     // Convert C# statements to Lua
                    // });
                }
            }
        }

        #endregion

        #region Collection Processing

        /// <summary>
        /// Recursively processes collection initializers, supporting nested lists and dictionaries.
        /// Converts C# collection syntax to Lua table syntax.
        /// </summary>
        /// <param name="initializer">The collection initializer expression</param>
        /// <param name="fieldName">Field name for error reporting</param>
        /// <returns>A List&lt;object&gt; or Dictionary&lt;string, object&gt; representing the collection</returns>
        /// <example>
        /// Simple list:
        /// <code>
        /// new() { 1, 2, 3 }
        /// Returns: List&lt;object&gt; { 1, 2, 3 }
        /// </code>
        /// 
        /// Dictionary:
        /// <code>
        /// new() { { "name", "John" }, { "age", "30" } }
        /// Returns: Dictionary&lt;string, object&gt; { ["name"] = "John", ["age"] = "30" }
        /// </code>
        /// 
        /// Nested list:
        /// <code>
        /// new() { new() { 1, 2 }, new() { 3, 4 } }
        /// Returns: List&lt;object&gt; { List { 1, 2 }, List { 3, 4 } }
        /// </code>
        /// 
        /// Mixed structure:
        /// <code>
        /// new() { 
        ///     { "items", new() { "sword", "shield" } },
        ///     { "count", "2" }
        /// }
        /// Returns: Dictionary with nested list
        /// </code>
        /// </example>
        private static object? ProcessInitializer(InitializerExpressionSyntax? initializer, string fieldName)
        {
            if (initializer == null)
                return null;

            List<object> items = new();

            // Process each expression in the initializer
            foreach (var expression in initializer.Expressions)
            {
                var item = ProcessCollectionExpression(expression, fieldName);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            // Convert to appropriate collection type (List or Dictionary)
            return ConvertToCollectionType(items);
        }

        /// <summary>
        /// Processes a single expression within a collection initializer.
        /// Routes to appropriate handler based on expression type.
        /// </summary>
        /// <example>
        /// Expression types:
        /// - ComplexElementInitializer: { "key", "value" } → Dictionary entry
        /// - CollectionInitializer: new() { 1, 2, 3 } → Nested collection
        /// - StringLiteral: "text" → Simple value
        /// - NumericLiteral: 42 → Simple value
        /// - ImplicitObjectCreation: new() { ... } → Nested object
        /// - ArrayCreation: new object[] { ... } → Array as list
        /// </example>
        private static object? ProcessCollectionExpression(ExpressionSyntax expression, string fieldName)
        {
            return expression.RawKind switch
            {
                // Dictionary entry: { "key", "value" }
                (int)SyntaxKind.ComplexElementInitializerExpression => ProcessDictionaryEntry(expression, fieldName),

                // Nested collection: new() { 1, 2, 3 } or { 1, 2, 3 }
                (int)SyntaxKind.CollectionInitializerExpression or
                (int)SyntaxKind.ArrayInitializerExpression => ProcessNestedCollection(expression, fieldName),

                // Simple values: "text", 42, true, false
                (int)SyntaxKind.StringLiteralExpression or
                (int)SyntaxKind.NumericLiteralExpression or
                (int)SyntaxKind.TrueLiteralExpression or
                (int)SyntaxKind.FalseLiteralExpression => ProcessLiteralValue(expression),

                // Implicit object creation: new() { ... }
                (int)SyntaxKind.ImplicitObjectCreationExpression => ProcessImplicitObjectCreation(expression, fieldName),

                // Array creation: new object[] { ... }
                (int)SyntaxKind.ArrayCreationExpression => ProcessArrayCreation(expression, fieldName),

                // Unsupported expression type
                _ => LogUnsupportedExpression(expression, fieldName)
            };
        }

        /// <summary>
        /// Processes a dictionary entry in the format { key, value }.
        /// Used for Dictionary&lt;TKey, TValue&gt; initializers.
        /// </summary>
        /// <example>
        /// C# Input: { "health", "100" }
        /// Output: KeyValuePair&lt;string, object&gt; ("health", "100")
        /// 
        /// With nested value:
        /// C# Input: { "items", new() { "sword", "shield" } }
        /// Output: KeyValuePair&lt;string, object&gt; ("items", List { "sword", "shield" })
        /// 
        /// With array value:
        /// C# Input: { "Key4", new object[] { "nested", 123, false } }
        /// Output: KeyValuePair&lt;string, object&gt; ("Key4", List { "nested", 123, false })
        /// </example>
        private static object? ProcessDictionaryEntry(ExpressionSyntax expression, string fieldName)
        {
            var cei = expression as InitializerExpressionSyntax;
            if (cei?.Expressions.Count < 2) return null;

            var keyExpr = cei.Expressions[0];
            var valueExpr = cei.Expressions[1];

            // Extract the key (must be a string literal)
            string? key = keyExpr switch
            {
                LiteralExpressionSyntax les => les.Token.ValueText,
                _ => null
            };

            if (key == null) return null;

            // Extract the value (can be any supported type, including nested collections)
            object? value = ExtractValue(valueExpr);
            return new KeyValuePair<string, object?>(key, value);
        }

        /// <summary>
        /// Processes a nested collection initializer recursively.
        /// </summary>
        /// <example>
        /// C# Input: new() { 1, 2, 3 }
        /// Output: List&lt;object&gt; { 1, 2, 3 }
        /// 
        /// Deeply nested:
        /// C# Input: new() { new() { 1, 2 }, new() { 3, 4 } }
        /// Output: List&lt;object&gt; { List { 1, 2 }, List { 3, 4 } }
        /// </example>
        private static object? ProcessNestedCollection(ExpressionSyntax expression, string fieldName)
        {
            var nestedInitializer = expression as InitializerExpressionSyntax;
            return ProcessInitializer(nestedInitializer, fieldName);
        }

        /// <summary>
        /// Extracts a literal value from an expression.
        /// </summary>
        /// <example>
        /// "Hello" → "Hello"
        /// 42 → 42
        /// true → true
        /// 3.14 → 3.14
        /// </example>
        private static object? ProcessLiteralValue(ExpressionSyntax expression)
        {
            var les = expression as LiteralExpressionSyntax;
            return les?.Token.Value;
        }

        /// <summary>
        /// Processes an implicit object creation expression: new() { ... }
        /// </summary>
        /// <example>
        /// C# Input: new() { "a", "b", "c" }
        /// Output: List&lt;object&gt; { "a", "b", "c" }
        /// </example>
        private static object? ProcessImplicitObjectCreation(ExpressionSyntax expression, string fieldName)
        {
            var implicitObj = expression as ImplicitObjectCreationExpressionSyntax;
            return ProcessInitializer(implicitObj?.Initializer, fieldName);
        }

        /// <summary>
        /// Processes an array creation expression: new Type[] { ... }
        /// Converts arrays to Lua tables (lists).
        /// </summary>
        /// <example>
        /// C# Input: new object[] { "nested value", 123, false }
        /// Output: List&lt;object&gt; { "nested value", 123, false }
        /// 
        /// C# Input: new string[] { "a", "b", "c" }
        /// Output: List&lt;object&gt; { "a", "b", "c" }
        /// </example>
        private static object? ProcessArrayCreation(ExpressionSyntax expression, string fieldName)
        {
            var arrayCreation = expression as ArrayCreationExpressionSyntax;
            return ProcessInitializer(arrayCreation?.Initializer, fieldName);
        }

        /// <summary>
        /// Logs a warning for unsupported expression types.
        /// </summary>
        private static object? LogUnsupportedExpression(ExpressionSyntax expression, string fieldName)
        {
            Logger.Log(LogSeverity.Warning, $"Unsupported collection entry type {expression.Kind()} in static field initializer for {fieldName}, skipping.");
            return null;
        }

        /// <summary>
        /// Converts a list of items into either a Dictionary or List based on content type.
        /// If all items are KeyValuePairs, creates a Dictionary. Otherwise, creates a List.
        /// </summary>
        /// <example>
        /// All KeyValuePairs:
        /// Input: [ KVP("a", 1), KVP("b", 2) ]
        /// Output: Dictionary { "a": 1, "b": 2 }
        /// 
        /// Mixed or non-KVP items:
        /// Input: [ 1, 2, 3 ]
        /// Output: List { 1, 2, 3 }
        /// </example>
        private static object ConvertToCollectionType(List<object> items)
        {
            if (items.Count == 0)
            {
                return new List<object>();
            }

            // If all items are key-value pairs, create a dictionary
            if (items.All(x => x is KeyValuePair<string, object?>))
            {
                return ConvertToDictionary(items);
            }
            else
            {
                // Otherwise, return as a list
                return items;
            }
        }

        /// <summary>
        /// Converts a list of KeyValuePairs into a Dictionary.
        /// </summary>
        /// <example>
        /// Input: List [ KVP("name", "John"), KVP("age", 30) ]
        /// Output: Dictionary { "name": "John", "age": 30 }
        /// </example>
        private static Dictionary<string, object?> ConvertToDictionary(List<object> items)
        {
            var dict = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> kvp in items)
            {
                dict[kvp.Key] = kvp.Value;
            }
            return dict;
        }

        /// <summary>
        /// Extracts values from expressions, handling nested structures.
        /// This is the main value extraction point that handles recursion.
        /// </summary>
        /// <param name="expression">The expression to extract a value from</param>
        /// <returns>The extracted value (primitive, List, or Dictionary)</returns>
        /// <example>
        /// Literal: "text" → "text"
        /// Nested list: new() { 1, 2 } → List { 1, 2 }
        /// Nested dict: new() { { "a", "1" } } → Dictionary { "a": "1" }
        /// Array: new object[] { 1, 2, 3 } → List { 1, 2, 3 }
        /// </example>
        private static object? ExtractValue(ExpressionSyntax expression)
        {
            return expression switch
            {
                // Simple literal value
                LiteralExpressionSyntax les => les.Token.Value,

                // Nested collection: { 1, 2, 3 }
                InitializerExpressionSyntax ies => ProcessInitializer(ies, "nested"),

                // Implicit object creation: new() { ... }
                ImplicitObjectCreationExpressionSyntax ioc => ProcessInitializer(ioc.Initializer, "nested"),

                // Array creation: new object[] { ... } or new string[] { ... }
                ArrayCreationExpressionSyntax ace => ProcessInitializer(ace.Initializer, "nested"),

                // Unsupported expression type
                _ => null
            };
        }

        #endregion

        /// <summary>
        /// Convert the class into the lua code.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            SourceConstructor.ClassBuilder builder = SourceConstructor.ClassBuilder.Create(this.ClassName);

            // iterate each constructor

            return builder.Build();
        }
    }
}
