namespace RoslynLuau;

/// <summary>
/// Multi-file orchestrator that resolves cross-module require() statements.
/// Wraps SimpleParser and SimpleEmitter to transpile a set of C# source files
/// to Luau, inserting the correct require() preamble in each output file so
/// that types defined in one module are properly imported into every module
/// that references them.
///
/// Self-hosting constraints: This file is transpiled to Luau by LuauEmitter.cs.
/// No LINQ, no string interpolation, no var, no is/as patterns, no ?. or ??,
/// no switch expressions. String output via plain string + concatenation.
/// No generic collections (List, Dictionary, HashSet) — plain arrays only.
/// No foreach — for (int i = ...) loops only.
/// </summary>
public class SimpleTranspiler
{
    // ── Type-to-module map ────────────────────────────────────────────────────
    // Parallel arrays: _typeNames[i] is defined in the module _moduleNames[i].
    // e.g. "ExpressionSyntax" → "SyntaxNode", "SyntaxKind" → "SyntaxKind"
    private string[] _typeNames;
    private string[] _moduleNames;
    private int _typeMapCount;

    // ── Ignored type names ────────────────────────────────────────────────────
    // These should never produce a require() statement.
    private string[] _ignoredTypes;
    private int _ignoredCount;

    public SimpleTranspiler()
    {
        _typeNames = new string[256];
        _moduleNames = new string[256];
        _typeMapCount = 0;

        _ignoredTypes = new string[32];
        _ignoredCount = 0;

        // Populate the ignored-type list with .NET built-ins and primitives that
        // are not Luau modules.
        _ignoredTypes[_ignoredCount] = "Math";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "Console";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "String";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "Int32";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "Convert";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "Char";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "CharUnicodeInfo";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "UnicodeCategory";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "ArgumentOutOfRangeException";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "string";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "int";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "bool";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "number";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "any";
        _ignoredCount++;
        _ignoredTypes[_ignoredCount] = "nil";
        _ignoredCount++;
    }

    // ── Phase 1: PreScan ──────────────────────────────────────────────────────

    /// <summary>
    /// Scan all source files to build the global type-to-module map.
    /// Must be called before TranspileAll or Transpile so that cross-file type
    /// references can be resolved to the correct require() path.
    /// </summary>
    public void PreScan(string[] sources, string[] fileNames, int fileCount)
    {
        _typeMapCount = 0;

        for (int i = 0; i < fileCount; i++)
        {
            // Derive module name from file name (strip path and extension).
            string moduleName = StripExtension(StripDirectory(fileNames[i]));

            // Parse the source to get the AST.
            SimpleParser parser = new SimpleParser(sources[i]);
            CompilationUnitSyntax unit = parser.ParseCompilationUnit();

            // Walk the AST and register every type found in this file.
            CollectTypeNames(unit, moduleName);
        }
    }

    // ── Phase 2: Transpile ────────────────────────────────────────────────────

    /// <summary>
    /// Transpile a single C# source file to Luau.
    /// Emits the file with SimpleEmitter, then performs a second pass to detect
    /// references to types defined in other modules and inserts the appropriate
    /// require() block after the file header.
    /// </summary>
    public string Transpile(string source, string fileName)
    {
        string moduleName = StripExtension(StripDirectory(fileName));

        // Gather the type names defined in THIS file so we don't require them.
        string[] localTypes = new string[64];
        int localCount = 0;

        SimpleParser scanParser = new SimpleParser(source);
        CompilationUnitSyntax scanUnit = scanParser.ParseCompilationUnit();

        for (int i = 0; i < scanUnit.Members.Length; i++)
        {
            int kind = scanUnit.Members[i].Kind;
            if (kind == 8855)
            {
                // ClassDeclaration
                ClassDeclarationSyntax cls = (ClassDeclarationSyntax)scanUnit.Members[i];
                if (localCount < 64)
                {
                    localTypes[localCount] = cls.Name;
                    localCount++;
                }
            }
            else if (kind == 8856)
            {
                // EnumDeclaration
                EnumDeclarationSyntax enm = (EnumDeclarationSyntax)scanUnit.Members[i];
                if (localCount < 64)
                {
                    localTypes[localCount] = enm.Name;
                    localCount++;
                }
            }
            else if (kind == 8857)
            {
                // StructDeclaration
                StructDeclarationSyntax str = (StructDeclarationSyntax)scanUnit.Members[i];
                if (localCount < 64)
                {
                    localTypes[localCount] = str.Name;
                    localCount++;
                }
            }
        }

        // Emit Luau via SimpleEmitter.
        SimpleParser emitParser = new SimpleParser(source);
        CompilationUnitSyntax emitUnit = emitParser.ParseCompilationUnit();
        SimpleEmitter emitter = new SimpleEmitter();
        string output = emitter.Emit(emitUnit);

        // Determine which external modules need to be required.
        // We collect unique module names and, for each module, the type names
        // from that module that appear in the output.

        // Skip the header comment lines when scanning for type references.
        // The header is "-- Auto-generated by SimpleEmitter" which would
        // falsely match the type name "SimpleEmitter" in every file.
        int headerEnd = 0;
        int newlineCount = 0;
        for (int h = 0; h < output.Length; h++)
        {
            if (output[h] == '\n')
            {
                newlineCount++;
                if (newlineCount >= 3)
                {
                    headerEnd = h + 1;
                    break;
                }
            }
        }

        // Unique module names referenced (up to 64 modules).
        string[] reqModules = new string[64];
        int reqModuleCount = 0;

        // For each module slot, store up to 32 type names.
        // reqTypes[m * 32 + t] = type name t in module slot m.
        string[] reqTypes = new string[64 * 32];
        int[] reqTypeCounts = new int[64];

        for (int i = 0; i < _typeMapCount; i++)
        {
            string typeName = _typeNames[i];
            string typeModule = _moduleNames[i];

            // Skip types defined in the same file.
            bool isLocal = false;
            for (int j = 0; j < localCount; j++)
            {
                if (localTypes[j] == typeName)
                {
                    isLocal = true;
                    break;
                }
            }
            if (isLocal) continue;

            // Skip ignored types.
            if (IsIgnoredType(typeName)) continue;

            // Skip types whose module is the same as this file's module.
            if (typeModule == moduleName) continue;

            // Check whether this type name appears in the emitted output (after header).
            if (!IsTypeReferenced(typeName, output, headerEnd)) continue;

            // Find or create a slot for this module.
            int moduleSlot = -1;
            for (int m = 0; m < reqModuleCount; m++)
            {
                if (reqModules[m] == typeModule)
                {
                    moduleSlot = m;
                    break;
                }
            }
            if (moduleSlot == -1)
            {
                if (reqModuleCount < 64)
                {
                    moduleSlot = reqModuleCount;
                    reqModules[reqModuleCount] = typeModule;
                    reqTypeCounts[reqModuleCount] = 0;
                    reqModuleCount++;
                }
            }

            // Add the type name to this module's slot (avoid duplicates).
            if (moduleSlot >= 0 && moduleSlot < 64)
            {
                int typeSlotBase = moduleSlot * 32;
                int typeCount = reqTypeCounts[moduleSlot];
                bool typeAlreadyAdded = false;
                for (int t = 0; t < typeCount; t++)
                {
                    if (reqTypes[typeSlotBase + t] == typeName)
                    {
                        typeAlreadyAdded = true;
                        break;
                    }
                }
                if (!typeAlreadyAdded && typeCount < 32)
                {
                    reqTypes[typeSlotBase + typeCount] = typeName;
                    reqTypeCounts[moduleSlot] = typeCount + 1;
                }
            }
        }

        // If no external modules were detected, return the emitted output unchanged.
        if (reqModuleCount == 0)
        {
            return output;
        }

        // Sort modules alphabetically (insertion sort).
        SortModuleSlots(reqModules, reqTypes, reqTypeCounts, reqModuleCount);

        // Sort each module's type list alphabetically.
        for (int m = 0; m < reqModuleCount; m++)
        {
            SortTypeSlot(reqTypes, reqTypeCounts[m], m * 32);
        }

        // Build the require block string.
        string requireBlock = BuildRequireBlock(reqModules, reqTypes, reqTypeCounts, reqModuleCount);

        // Insert the require block after the file header.
        return InsertRequires(output, requireBlock);
    }

    /// <summary>
    /// Transpile all files. PreScan must be called first.
    /// Returns an array of Luau source strings in the same order as the inputs.
    /// </summary>
    public string[] TranspileAll(string[] sources, string[] fileNames, int fileCount)
    {
        string[] results = new string[fileCount];
        for (int i = 0; i < fileCount; i++)
        {
            results[i] = Transpile(sources[i], fileNames[i]);
        }
        return results;
    }

    // ── AST helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Walk a CompilationUnitSyntax and register every top-level type name it
    /// defines into the global type-to-module map.
    /// Kind 8855 = ClassDeclaration, 8856 = EnumDeclaration, 8857 = StructDeclaration.
    /// </summary>
    private void CollectTypeNames(CompilationUnitSyntax unit, string moduleName)
    {
        for (int i = 0; i < unit.Members.Length; i++)
        {
            MemberDeclarationSyntax member = unit.Members[i];
            int kind = member.Kind;

            if (kind == 8855)
            {
                // ClassDeclaration
                ClassDeclarationSyntax cls = (ClassDeclarationSyntax)member;
                RegisterType(cls.Name, moduleName);
            }
            else if (kind == 8856)
            {
                // EnumDeclaration
                EnumDeclarationSyntax enm = (EnumDeclarationSyntax)member;
                RegisterType(enm.Name, moduleName);
            }
            else if (kind == 8857)
            {
                // StructDeclaration
                StructDeclarationSyntax str = (StructDeclarationSyntax)member;
                RegisterType(str.Name, moduleName);
            }
        }
    }

    /// <summary>
    /// Add a (typeName, moduleName) entry to the global map, avoiding duplicates.
    /// If the type name was already registered, the existing mapping is kept (first
    /// file to declare a type wins).
    /// </summary>
    private void RegisterType(string typeName, string moduleName)
    {
        // Check for an existing entry.
        for (int i = 0; i < _typeMapCount; i++)
        {
            if (_typeNames[i] == typeName)
            {
                // Already registered — keep the first mapping.
                return;
            }
        }

        if (_typeMapCount < 256)
        {
            _typeNames[_typeMapCount] = typeName;
            _moduleNames[_typeMapCount] = moduleName;
            _typeMapCount++;
        }
    }

    // ── Reference detection ───────────────────────────────────────────────────

    /// <summary>
    /// Return true if typeName appears as a whole word in emittedOutput after startOffset.
    /// A "whole word" occurrence is one where the character immediately before and
    /// immediately after the match are not alphanumeric and are not underscores.
    /// The search is case-sensitive (Luau identifiers are case-sensitive).
    /// startOffset is used to skip the header comment lines (which contain "SimpleEmitter").
    /// </summary>
    private bool IsTypeReferenced(string typeName, string emittedOutput, int startOffset)
    {
        if (typeName == null) return false;
        if (emittedOutput == null) return false;
        int nameLen = typeName.Length;
        if (nameLen == 0) return false;
        int outputLen = emittedOutput.Length;
        if (outputLen < nameLen) return false;

        int searchFrom = startOffset;
        while (searchFrom <= outputLen - nameLen)
        {
            // Find the next occurrence of the first character.
            int idx = IndexOf(emittedOutput, typeName, searchFrom);
            if (idx == -1) return false;

            // Check the character before the match.
            bool leftOk = (idx == 0) || !IsWordChar(emittedOutput[idx - 1]);

            // Check the character after the match.
            int afterIdx = idx + nameLen;
            bool rightOk = (afterIdx >= outputLen) || !IsWordChar(emittedOutput[afterIdx]);

            if (leftOk && rightOk) return true;

            // Advance past this non-word occurrence and keep searching.
            searchFrom = idx + 1;
        }

        return false;
    }

    /// <summary>
    /// Return the index of needle in haystack starting at startIndex, or -1 if not found.
    /// Hand-rolled because we cannot use System.String.IndexOf in transpiler-safe code
    /// (actually we can — it is a built-in — but the implementation is kept explicit for
    /// clarity in the self-hosted context).
    /// </summary>
    private int IndexOf(string haystack, string needle, int startIndex)
    {
        int haystackLen = haystack.Length;
        int needleLen = needle.Length;
        int limit = haystackLen - needleLen;

        for (int i = startIndex; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < needleLen; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Return true if c is a character that can appear inside a Luau identifier
    /// (letter, digit, or underscore).
    /// </summary>
    private bool IsWordChar(char c)
    {
        if (c >= 'a' && c <= 'z') return true;
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= '0' && c <= '9') return true;
        if (c == '_') return true;
        return false;
    }

    // ── Require insertion ─────────────────────────────────────────────────────

    /// <summary>
    /// Insert requireBlock into emittedOutput immediately after the two-line header
    /// emitted by SimpleEmitter:
    ///   --!strict\n
    ///   -- Auto-generated by SimpleEmitter\n
    ///   \n
    /// The require block is inserted in place of (i.e., before) the first real
    /// content line, so the final file looks like:
    ///   --!strict
    ///   -- Auto-generated by SimpleEmitter
    ///
    ///   local _SyntaxNode = require(script.Parent.SyntaxNode)
    ///   ...
    ///
    ///   &lt;rest of emitted code&gt;
    /// </summary>
    private string InsertRequires(string emittedOutput, string requireBlock)
    {
        // The header sentinel we look for is the blank line after the second header line.
        // SimpleEmitter emits: AppendLine("--!strict"), AppendLine("-- Auto-generated by SimpleEmitter"), AppendLine("")
        // Which produces: "--!strict\n-- Auto-generated by SimpleEmitter\n\n"
        // We want to insert AFTER that trailing "\n".
        string sentinel = "-- Auto-generated by SimpleEmitter\n";
        int sentinelLen = sentinel.Length;
        int sentinelIdx = IndexOf(emittedOutput, sentinel, 0);

        if (sentinelIdx == -1)
        {
            // No header found — prepend the require block at the top.
            return requireBlock + emittedOutput;
        }

        // Move past the sentinel line itself.
        int insertionPoint = sentinelIdx + sentinelLen;

        // Skip the blank line that follows the sentinel (AppendLine("") emits "\n").
        if (insertionPoint < emittedOutput.Length && emittedOutput[insertionPoint] == '\n')
        {
            insertionPoint++;
        }

        // Split the output at the insertion point and splice in the require block.
        string before = emittedOutput.Substring(0, insertionPoint);
        string after = emittedOutput.Substring(insertionPoint);
        return before + requireBlock + after;
    }

    // ── Require block builder ─────────────────────────────────────────────────

    /// <summary>
    /// Build the Luau require block string for a sorted set of modules and their types.
    /// For each module, emits:
    ///   local _ModuleName = require(script.Parent.ModuleName)
    ///   local TypeA = _ModuleName.TypeA
    ///   local TypeB = _ModuleName.TypeB
    /// Followed by a trailing blank line.
    /// </summary>
    private string BuildRequireBlock(string[] modules, string[] types, int[] typeCounts, int moduleCount)
    {
        string result = "";
        for (int m = 0; m < moduleCount; m++)
        {
            string modName = modules[m];
            // Use deferred lazy proxy to avoid circular require() errors
            result = result + "local _" + modName + "\n";

            int typeSlotBase = m * 32;
            int typeCount = typeCounts[m];
            for (int t = 0; t < typeCount; t++)
            {
                string typeName = types[typeSlotBase + t];
                result = result + "local " + typeName + " = setmetatable({}, {__index = function(_, k)\n";
                result = result + "\tif not _" + modName + " then _" + modName + " = require(script.Parent." + modName + ") end\n";
                result = result + "\treturn _" + modName + "." + typeName + "[k]\n";
                result = result + "end})\n";
            }
        }
        result = result + "\n";
        return result;
    }

    // ── Sorting helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Sort the module slots alphabetically by module name using insertion sort.
    /// The parallel type arrays and count arrays are reordered to match.
    /// </summary>
    private void SortModuleSlots(string[] modules, string[] types, int[] typeCounts, int count)
    {
        // Insertion sort on modules[], mirroring swaps in types[] and typeCounts[].
        for (int i = 1; i < count; i++)
        {
            string keyModule = modules[i];
            int keyCount = typeCounts[i];

            // Copy type slice for slot i into a temp buffer.
            string[] keyTypes = new string[32];
            int slotBase = i * 32;
            for (int t = 0; t < keyCount; t++)
            {
                keyTypes[t] = types[slotBase + t];
            }

            int j = i - 1;
            while (j >= 0 && StringGreaterThan(modules[j], keyModule))
            {
                // Shift slot j up to slot j+1.
                modules[j + 1] = modules[j];
                typeCounts[j + 1] = typeCounts[j];

                int srcBase = j * 32;
                int dstBase = (j + 1) * 32;
                int srcCount = typeCounts[j + 1]; // already updated above
                for (int t = 0; t < srcCount; t++)
                {
                    types[dstBase + t] = types[srcBase + t];
                }

                j--;
            }

            // Place the key at position j+1.
            modules[j + 1] = keyModule;
            typeCounts[j + 1] = keyCount;
            int destBase = (j + 1) * 32;
            for (int t = 0; t < keyCount; t++)
            {
                types[destBase + t] = keyTypes[t];
            }
        }
    }

    /// <summary>
    /// Sort the type name entries for a single module slot alphabetically.
    /// Operates on a sub-slice of the types array starting at slotBase.
    /// </summary>
    private void SortTypeSlot(string[] types, int count, int slotBase)
    {
        // Insertion sort on the type sub-slice.
        for (int i = 1; i < count; i++)
        {
            string key = types[slotBase + i];
            int j = i - 1;
            while (j >= 0 && StringGreaterThan(types[slotBase + j], key))
            {
                types[slotBase + j + 1] = types[slotBase + j];
                j--;
            }
            types[slotBase + j + 1] = key;
        }
    }

    /// <summary>
    /// Return true if a is lexicographically greater than b (ordinal comparison).
    /// Used by the insertion sort helpers.
    /// </summary>
    private bool StringGreaterThan(string a, string b)
    {
        int aLen = a.Length;
        int bLen = b.Length;
        int minLen = aLen;
        if (bLen < minLen) minLen = bLen;

        for (int i = 0; i < minLen; i++)
        {
            if (a[i] > b[i]) return true;
            if (a[i] < b[i]) return false;
        }

        // Common prefix — longer string is greater.
        return aLen > bLen;
    }

    // ── Ignored-type check ────────────────────────────────────────────────────

    /// <summary>
    /// Return true if typeName should never produce a require() statement.
    /// Covers .NET BCL types that appear in the source but have no Luau module.
    /// </summary>
    private bool IsIgnoredType(string typeName)
    {
        for (int i = 0; i < _ignoredCount; i++)
        {
            if (_ignoredTypes[i] == typeName) return true;
        }
        return false;
    }

    // ── File name helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Return the file name component of a path (everything after the last '/' or '\').
    /// If neither separator is present, returns the whole string.
    /// </summary>
    private string StripDirectory(string path)
    {
        int lastSlash = -1;
        for (int i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] == '/' || path[i] == '\\')
            {
                lastSlash = i;
                break;
            }
        }
        if (lastSlash == -1) return path;
        return path.Substring(lastSlash + 1);
    }

    /// <summary>
    /// Return the string with the last '.' and everything after it removed.
    /// If no '.' is present, returns the whole string unchanged.
    /// e.g. "SyntaxNode.cs" → "SyntaxNode", "SimpleParser" → "SimpleParser"
    /// </summary>
    private string StripExtension(string name)
    {
        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (name[i] == '.')
            {
                return name.Substring(0, i);
            }
        }
        return name;
    }
}
