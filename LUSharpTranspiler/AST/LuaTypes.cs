using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LUSharpTranspiler.AST
{
    public class LuaTypes
    {
        /// <summary>
        /// Built-in data types for LuaU
        /// </summary>
        public enum LuaDataType
        {
            /// <summary>
            /// No value
            /// </summary>
            Nil,
            /// <summary>
            /// Text, multi-byte represented data
            /// </summary>
            String,
            /// <summary>
            /// An numerical value.
            /// </summary>
            Number,
            /// <summary>
            /// A true or false value.
            /// </summary>
            Boolean,
            /// <summary>
            /// A collection of datatypes
            /// </summary>
            Table,
            /// <summary>
            /// A function
            /// </summary>
            Function,
            /// <summary>
            /// A thread, idk what for yet
            /// </summary>
            Thread,
            /// <summary>
            /// Aribitrary data
            /// </summary>
            Userdata,
            Vector,
            Buffer,

            /* Type checker related 
             https://luau.org/typecheck#builtin-types
             */

            /// <summary>
            /// unknown is also said to be the top type, that is it’s a union of all types. 
            /// </summary>
            Unknown,
            /// <summary>
            /// never is also said to be the bottom type, meaning there doesn’t exist a value that inhabits the type never. In fact, it is the dual of unknown. never is useful in many scenarios, and one such use case is when type refinements proves it impossible:
            /// <code>local x = unknown()
            /// if typeof(x) == "number" and typeof(x) == "string" then
            ///     -- x : never
            /// end
            /// </code>
            /// </summary>
            Never,
            /// <summary>
            /// any is just like unknown, except that it allows itself to be used as an arbitrary type without further checks or annotations. Essentially, it’s an opt-out from the type system entirely.
            /// <code>
            /// local x: any = 5
            /// local y: string = x -- no type errors here!
            /// </code>
            /// </summary>
            Any
        }
    
        /// <summary>
        /// Types of tables for LuaU
        /// </summary>
        public enum LuaTableType
        {
            /// <summary>
            /// An unsealed table is a table which supports adding new properties, which updates the tables type. Unsealed tables are created using table literals. This is one way to accumulate knowledge of the shape of this table.
            /// </summary>
            Unsealed,
            /// <summary>
            /// A sealed table is a table that is now locked down. This occurs when the table type is spelled out explicitly via a type annotation, or if it is returned from a function.
            /// </summary>
            Sealed,
            /// <summary>
            /// This typically occurs when the symbol does not have any annotated types or were not inferred anything concrete. In this case, when you index on a parameter, you’re requesting that there is a table with a matching interface.
            /// </summary>
            Generic
        }
    
        public enum LuaOtherTypes
        {
            /// <summary>
            /// A union type represents one of the types in this set. 
            /// If you try to pass a union onto another thing that expects a more specific type, it will fail.
            /// For example, what if this string | number was passed into something that expects number, but the passed in value was actually a string?
            /// 
            /// <code>
            /// local stringOrNumber: string | number = "foo"
            /// local onlyString: string = stringOrNumber -- not ok
            /// local onlyNumber: number = stringOrNumber-- not ok
            /// </code>
            /// </summary>
            Union,

            /// <summary>
            /// An intersection type represents all of the types in this set. It’s useful for two main things: to join multiple tables together, or to specify overloadable functions.
            /// <code>
            /// type XCoord = {x: number}
            /// type YCoord = {y: number}
            /// type ZCoord = {z: number}
            /// type Vector2 = XCoord & YCoord
            /// type Vector3 = XCoord & YCoord & ZCoord
            /// local vec2: Vector2 = {x = 1, y = 2}        -- ok
            /// local vec3: Vector3 = {x = 1, y = 2, z = 3} -- ok
            /// type SimpleOverloadedFunction = ((string) -> number) & ((number) -> string)
            /// local f: SimpleOverloadedFunction
            /// local r1: number = f("foo") -- ok
            /// local r2: number = f(12345) -- not ok
            /// local r3: string = f("foo") -- not ok
            /// local r4: string = f(12345) -- ok
            /// </code>
            /// </summary>
            Intersection,

            ///<summary>
            /// Luau’s type system also supports singleton types, which means it’s a type that represents one single value at runtime. At this time, both string and booleans are representable in types.
            /// We do not currently support numbers as types. For now, this is intentional.
            /// <code>
            /// local foo: "Foo" = "Foo" -- ok
            /// local bar: "Bar" = foo   -- not ok
            /// local baz: string = foo  -- ok
            /// local t: true = true -- ok
            /// local f: false = false -- ok
            /// </code>
            /// </summary>
            Singleton,

            /// <summary>
            /// Luau permits assigning a type to the ... variadic symbol like any other parameter:
            /// <code>
            /// local function f(...: number)
            /// end
            /// f(1, 2, 3)     -- ok
            /// f(1, "string") -- not ok
            /// </code>
            /// </summary>
            Variadic,

            /// <summary>
            /// Multiple function return values as well as the function variadic parameter use a type pack to represent a list of types.
            /// When a type alias is defined, generic type pack parameters can be used after the type parameters:
            /// Keep in mind that ...T is a variadic type pack (many elements of the same type T), while U... is a generic type pack that can contain zero or more types and they don’t have to be the same.
            /// <code>
            /// type Signal<T, U...> = { f: (T, U...) -> (), data: T }
            /// </code>
            /// </summary>
            TypePacks,

            /// <summary>
            /// Tagged unions are just union types! In particular, they’re union types of tables where they have at least some common properties but the structure of the tables are different enough. Here’s one example:
            /// <code>
            /// type Ok<T> = { type: "ok", value: T }
            /// type Err<E> = { type: "err", error: E }
            /// type Result<T, E> = Ok<T> | Err<E>
            /// </code>
            /// </summary>
            TaggedUnions
        }

    }
}
