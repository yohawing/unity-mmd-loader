#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace Mmd.Tests
{
    /// <summary>
    /// Compares an assembly's <see cref="PublicApiSnapshot"/> with a committed
    /// <c>&lt;Assembly&gt;.api.txt</c> baseline. Baselines are never rewritten by a
    /// normal run; set <c>YMU_GENERATE_API_BASELINE=1</c> to write candidates under
    /// <c>artifacts/api-baseline-candidates/</c> and copy them by hand.
    /// </summary>
    internal static class PublicApiBaselineAssert
    {
        internal const string GenerateBaselineVariable = "YMU_GENERATE_API_BASELINE";
        private const int MaxReportedLinesPerSide = 40;

        internal static string CandidateRoot =>
            Path.Combine(MmdTestFixtures.RepositoryRoot, "artifacts", "api-baseline-candidates");

        internal static Assembly RequireLoadedAssembly(string assemblyName)
        {
            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal));
            Assert.That(assembly, Is.Not.Null, "Assembly is not loaded in the editor AppDomain: " + assemblyName);
            return assembly!;
        }

        internal static void AssertMatchesBaseline(Assembly assembly, string baselinePath)
        {
            string assemblyName = assembly.GetName().Name;
            List<string> actual = PublicApiSnapshot.Build(assembly);
            Assert.That(actual, Is.Not.Empty, assemblyName + " exposes no public API; the snapshot would be meaningless.");

            string? candidatePath = null;
            if (string.Equals(Environment.GetEnvironmentVariable(GenerateBaselineVariable), "1", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(CandidateRoot);
                candidatePath = Path.Combine(CandidateRoot, assemblyName + ".api.txt");
                File.WriteAllText(candidatePath, string.Concat(actual.Select(line => line + "\n")), new UTF8Encoding(false));
            }

            string candidateHint = candidatePath != null
                ? "Candidate written to: " + candidatePath + ". Review it and copy it to " + baselinePath + " by hand."
                : "Set " + GenerateBaselineVariable + "=1 to write a candidate under " + CandidateRoot + ", then copy it to the baseline by hand.";

            if (!File.Exists(baselinePath))
            {
                Assert.Fail("Public API baseline is missing: " + baselinePath + "\n" + candidateHint);
            }

            List<string> expected = ReadBaseline(baselinePath);
            if (expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                return;
            }

            Assert.Fail(DescribeDifference(assemblyName, baselinePath, expected, actual) + "\n" + candidateHint);
        }

        private static List<string> ReadBaseline(string path)
        {
            return File.ReadAllText(path)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Where(line => line.Length > 0)
                .ToList();
        }

        private static string DescribeDifference(
            string assemblyName,
            string baselinePath,
            IReadOnlyList<string> expected,
            IReadOnlyList<string> actual)
        {
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
            List<string> added = actual.Where(line => !expectedSet.Contains(line)).Distinct(StringComparer.Ordinal).ToList();
            List<string> removed = expected.Where(line => !actualSet.Contains(line)).Distinct(StringComparer.Ordinal).ToList();

            var message = new StringBuilder();
            message.Append("Public API of ").Append(assemblyName).Append(" differs from ").Append(baselinePath).Append('\n');
            message.Append("Added: ").Append(added.Count).Append(", removed: ").Append(removed.Count).Append('\n');
            AppendLines(message, "+ ", added);
            AppendLines(message, "- ", removed);
            if (added.Count == 0 && removed.Count == 0)
            {
                message.Append("Line sets are equal but order or duplicates differ (baseline must be Ordinal-sorted and unique).\n");
            }

            return message.ToString();
        }

        private static void AppendLines(StringBuilder message, string prefix, IReadOnlyList<string> lines)
        {
            foreach (string line in lines.Take(MaxReportedLinesPerSide))
            {
                message.Append(prefix).Append(line).Append('\n');
            }

            if (lines.Count > MaxReportedLinesPerSide)
            {
                message.Append(prefix).Append("... ").Append(lines.Count - MaxReportedLinesPerSide).Append(" more\n");
            }
        }
    }

    /// <summary>
    /// Deterministic text rendering of an assembly's visible API. Every line is
    /// prefixed with the declaring type so a diff line is self-describing.
    /// Parameters render as <c>[ref|out|in|params ]Type name[ = default]</c>.
    /// </summary>
    internal static class PublicApiSnapshot
    {
        private const BindingFlags DeclaredMembers =
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        private static readonly Dictionary<Type, string> Keywords = new Dictionary<Type, string>
        {
            [typeof(void)] = "void",
            [typeof(object)] = "object",
            [typeof(string)] = "string",
            [typeof(bool)] = "bool",
            [typeof(char)] = "char",
            [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(decimal)] = "decimal",
            [typeof(IntPtr)] = "nint",
            [typeof(UIntPtr)] = "nuint"
        };

        internal static List<string> Build(Assembly assembly)
        {
            var lines = new List<string>();
            foreach (Type type in LoadTypes(assembly).Where(IsVisibleApiType))
            {
                AppendType(lines, type);
            }

            lines.Sort(StringComparer.Ordinal);
            return lines.Distinct(StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<Type> LoadTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                throw new InvalidOperationException(
                    "Could not load all types of " + assembly.GetName().Name + ": " +
                    string.Join("; ", exception.LoaderExceptions.Where(e => e != null).Select(e => e!.Message)),
                    exception);
            }
        }

        private static bool IsVisibleApiType(Type type)
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                return false;
            }

            if (!type.IsNested)
            {
                return type.IsPublic;
            }

            return (type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem) &&
                   IsVisibleApiType(type.DeclaringType!);
        }

        private static void AppendType(List<string> lines, Type type)
        {
            string owner = TypeDefinitionName(type) + " :: ";
            lines.Add(owner + "(type) " + DescribeTypeDeclaration(type));

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                return;
            }

            if (type.IsEnum)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    lines.Add(owner + "enum " + field.Name + " = " + FormatInvariant(field.GetRawConstantValue()));
                }

                return;
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(DeclaredMembers))
            {
                if (constructor.IsStatic || !IsVisible(constructor) || IsCompilerGenerated(constructor))
                {
                    continue;
                }

                lines.Add(owner + "ctor " + Visibility(constructor) + Obsolete(constructor) + " .ctor(" + FormatParameters(constructor.GetParameters()) + ")");
            }

            foreach (MethodInfo method in type.GetMethods(DeclaredMembers))
            {
                if (!IsVisible(method) || IsCompilerGenerated(method) ||
                    (method.IsSpecialName && !method.Name.StartsWith("op_", StringComparison.Ordinal)))
                {
                    continue;
                }

                lines.Add(owner + "method " + Visibility(method) + Obsolete(method) + MethodModifiers(method, type) +
                          FormatReturnType(method) + " " + method.Name + FormatGenericParameters(method.GetGenericArguments()) +
                          "(" + FormatParameters(method.GetParameters()) + ")" + FormatConstraints(method.GetGenericArguments()));
            }

            foreach (PropertyInfo property in type.GetProperties(DeclaredMembers))
            {
                string? line = DescribeProperty(property, type);
                if (line != null)
                {
                    lines.Add(owner + line);
                }
            }

            foreach (FieldInfo field in type.GetFields(DeclaredMembers))
            {
                if (field.IsSpecialName || IsCompilerGenerated(field) || !(field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly))
                {
                    continue;
                }

                string visibility = field.IsPublic ? "public" : "protected";
                if (field.IsLiteral)
                {
                    lines.Add(owner + "const " + visibility + Obsolete(field) + " " + FormatType(field.FieldType) + " " + field.Name +
                              " = " + FormatConstant(field.GetRawConstantValue(), field.FieldType));
                    continue;
                }

                string modifiers = (field.IsStatic ? " static" : string.Empty) + (field.IsInitOnly ? " readonly" : string.Empty);
                lines.Add(owner + "field " + visibility + Obsolete(field) + modifiers + " " + FormatType(field.FieldType) + " " + field.Name);
            }

            foreach (EventInfo evt in type.GetEvents(DeclaredMembers))
            {
                MethodInfo? add = evt.GetAddMethod(true);
                if (add == null || !IsVisible(add) || IsCompilerGenerated(evt))
                {
                    continue;
                }

                lines.Add(owner + "event " + Visibility(add) + Obsolete(evt) + MethodModifiers(add, type) +
                          FormatType(evt.EventHandlerType!) + " " + evt.Name);
            }
        }

        private static string DescribeTypeDeclaration(Type type)
        {
            var builder = new StringBuilder();
            builder.Append(type.IsPublic || type.IsNestedPublic ? "public" : "protected");
            builder.Append(Obsolete(type));
            if (type.IsDefined(typeof(FlagsAttribute), false))
            {
                builder.Append(" [Flags]");
            }

            if (typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate) && type != typeof(MulticastDelegate))
            {
                MethodInfo invoke = type.GetMethod("Invoke")!;
                builder.Append(" delegate ").Append(FormatReturnType(invoke)).Append(' ')
                    .Append(SimpleName(type)).Append(FormatGenericParameters(OwnGenericParameters(type)))
                    .Append('(').Append(FormatParameters(invoke.GetParameters())).Append(')')
                    .Append(FormatConstraints(OwnGenericParameters(type)));
                return builder.ToString();
            }

            if (type.IsEnum)
            {
                builder.Append(" enum ").Append(SimpleName(type)).Append(" : ").Append(FormatType(Enum.GetUnderlyingType(type)));
                return builder.ToString();
            }

            if (type.IsInterface)
            {
                builder.Append(" interface");
            }
            else if (type.IsValueType)
            {
                if (HasAttribute(type, "System.Runtime.CompilerServices.IsReadOnlyAttribute"))
                {
                    builder.Append(" readonly");
                }

                if (type.IsByRefLike)
                {
                    builder.Append(" ref");
                }

                builder.Append(" struct");
            }
            else
            {
                if (type.IsAbstract && type.IsSealed)
                {
                    builder.Append(" static");
                }
                else if (type.IsAbstract)
                {
                    builder.Append(" abstract");
                }
                else if (type.IsSealed)
                {
                    builder.Append(" sealed");
                }

                builder.Append(" class");
            }

            Type[] ownGenerics = OwnGenericParameters(type);
            builder.Append(' ').Append(SimpleName(type)).Append(FormatGenericParameters(ownGenerics));

            var bases = new List<string>();
            Type? baseType = type.BaseType;
            if (baseType != null && baseType != typeof(object) && baseType != typeof(ValueType))
            {
                bases.Add(FormatType(baseType));
            }

            HashSet<Type> inherited = baseType != null
                ? new HashSet<Type>(baseType.GetInterfaces())
                : new HashSet<Type>();
            bases.AddRange(type.GetInterfaces()
                .Where(candidate => !inherited.Contains(candidate) && IsVisibleInterface(candidate))
                .Select(FormatType)
                .OrderBy(name => name, StringComparer.Ordinal));
            if (bases.Count > 0)
            {
                builder.Append(" : ").Append(string.Join(", ", bases));
            }

            builder.Append(FormatConstraints(ownGenerics));
            return builder.ToString();
        }

        private static bool IsVisibleInterface(Type type)
        {
            Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            return definition.IsPublic || definition.IsNestedPublic;
        }

        private static string? DescribeProperty(PropertyInfo property, Type owner)
        {
            if (IsCompilerGenerated(property))
            {
                return null;
            }

            MethodInfo? getter = property.GetGetMethod(true);
            MethodInfo? setter = property.GetSetMethod(true);
            bool getterVisible = getter != null && IsVisible(getter);
            bool setterVisible = setter != null && IsVisible(setter);
            if (!getterVisible && !setterVisible)
            {
                return null;
            }

            MethodInfo primary = getterVisible ? getter! : setter!;
            var accessors = new List<string>();
            if (getterVisible)
            {
                accessors.Add(Visibility(getter!) + " get;");
            }

            if (setterVisible)
            {
                bool isInit = setter!.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
                accessors.Add(Visibility(setter) + (isInit ? " init;" : " set;"));
            }

            ParameterInfo[] indexParameters = property.GetIndexParameters();
            string name = indexParameters.Length > 0
                ? "this[" + FormatParameters(indexParameters) + "]"
                : property.Name;
            return "property" + Obsolete(property) + MethodModifiers(primary, owner).TrimEnd() + " " +
                   FormatType(property.PropertyType) + " " + name + " { " + string.Join(" ", accessors) + " }";
        }

        private static bool IsVisible(MethodBase method) =>
            method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

        private static string Visibility(MethodBase method) => method.IsPublic ? "public" : "protected";

        private static bool IsCompilerGenerated(MemberInfo member) =>
            member.IsDefined(typeof(CompilerGeneratedAttribute), false);

        private static string Obsolete(MemberInfo member)
        {
            ObsoleteAttribute? obsolete = member.GetCustomAttribute<ObsoleteAttribute>(false);
            if (obsolete == null)
            {
                return string.Empty;
            }

            return obsolete.IsError ? " [Obsolete(error)]" : " [Obsolete]";
        }

        private static bool HasAttribute(MemberInfo member, string fullName) =>
            member.GetCustomAttributesData().Any(data => data.AttributeType.FullName == fullName);

        private static string MethodModifiers(MethodInfo method, Type owner)
        {
            var builder = new StringBuilder(" ");
            if (method.IsStatic)
            {
                builder.Append("static ");
            }

            if (owner.IsInterface)
            {
                return builder.ToString();
            }

            bool newSlot = (method.Attributes & MethodAttributes.NewSlot) != 0;
            if (method.IsAbstract)
            {
                builder.Append(newSlot ? "abstract " : "abstract override ");
            }
            else if (method.IsVirtual)
            {
                if (newSlot && !method.IsFinal)
                {
                    builder.Append("virtual ");
                }
                else if (!newSlot)
                {
                    builder.Append(method.IsFinal ? "sealed override " : "override ");
                }
            }

            return builder.ToString();
        }

        private static string FormatReturnType(MethodInfo method)
        {
            Type returnType = method.ReturnType;
            if (returnType.IsByRef)
            {
                bool readOnly = method.ReturnParameter.GetCustomAttributesData()
                    .Any(data => data.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
                return (readOnly ? "ref readonly " : "ref ") + FormatType(returnType.GetElementType()!);
            }

            return FormatType(returnType);
        }

        private static string FormatParameters(ParameterInfo[] parameters)
        {
            return string.Join(", ", parameters.Select(FormatParameter));
        }

        private static string FormatParameter(ParameterInfo parameter)
        {
            var builder = new StringBuilder();
            Type parameterType = parameter.ParameterType;
            if (parameterType.IsByRef)
            {
                if (parameter.IsOut && !parameter.IsIn)
                {
                    builder.Append("out ");
                }
                else if (parameter.IsIn)
                {
                    builder.Append("in ");
                }
                else
                {
                    builder.Append("ref ");
                }

                parameterType = parameterType.GetElementType()!;
            }
            else if (parameter.IsDefined(typeof(ParamArrayAttribute), false))
            {
                builder.Append("params ");
            }

            builder.Append(FormatType(parameterType));
            if (!string.IsNullOrEmpty(parameter.Name))
            {
                builder.Append(' ').Append(parameter.Name);
            }

            if (parameter.HasDefaultValue)
            {
                builder.Append(" = ").Append(FormatConstant(parameter.RawDefaultValue, parameterType));
            }

            return builder.ToString();
        }

        private static string FormatGenericParameters(Type[] parameters)
        {
            return parameters.Length == 0
                ? string.Empty
                : "<" + string.Join(", ", parameters.Select(parameter => parameter.Name)) + ">";
        }

        private static string FormatConstraints(Type[] parameters)
        {
            var clauses = new List<string>();
            foreach (Type parameter in parameters)
            {
                if (!parameter.IsGenericParameter)
                {
                    continue;
                }

                GenericParameterAttributes attributes = parameter.GenericParameterAttributes;
                var constraints = new List<string>();
                bool isStruct = (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;
                if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                {
                    constraints.Add("class");
                }

                if (isStruct)
                {
                    constraints.Add("struct");
                }

                constraints.AddRange(parameter.GetGenericParameterConstraints()
                    .Where(constraint => !(isStruct && constraint == typeof(ValueType)))
                    .Select(FormatType)
                    .OrderBy(name => name, StringComparer.Ordinal));
                if (!isStruct && (attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                {
                    constraints.Add("new()");
                }

                if (constraints.Count > 0)
                {
                    clauses.Add(" where " + parameter.Name + " : " + string.Join(", ", constraints));
                }
            }

            return string.Concat(clauses);
        }

        private static Type[] OwnGenericParameters(Type type)
        {
            Type[] all = type.GetGenericArguments();
            int inherited = type.IsNested ? type.DeclaringType!.GetGenericArguments().Length : 0;
            return all.Skip(inherited).ToArray();
        }

        private static string SimpleName(Type type)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            return tick >= 0 ? name.Substring(0, tick) : name;
        }

        private static string TypeDefinitionName(Type type)
        {
            string prefix = type.IsNested
                ? TypeDefinitionName(type.DeclaringType!) + "."
                : string.IsNullOrEmpty(type.Namespace) ? string.Empty : type.Namespace + ".";
            Type[] ownGenerics = OwnGenericParameters(type);
            return prefix + SimpleName(type) + FormatGenericParameters(ownGenerics);
        }

        internal static string FormatType(Type type)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsByRef)
            {
                return "ref " + FormatType(type.GetElementType()!);
            }

            if (type.IsPointer)
            {
                return FormatType(type.GetElementType()!) + "*";
            }

            if (type.IsArray)
            {
                int rank = type.GetArrayRank();
                return FormatType(type.GetElementType()!) + "[" + new string(',', rank - 1) + "]";
            }

            if (Keywords.TryGetValue(type, out string? keyword))
            {
                return keyword!;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return FormatType(type.GetGenericArguments()[0]) + "?";
            }

            Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            Type[] arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            return FormatTypeName(definition, arguments, arguments.Length);
        }

        private static string FormatTypeName(Type definition, Type[] arguments, int argumentEnd)
        {
            int ownCount = OwnGenericParameters(definition).Length;
            int ownStart = argumentEnd - ownCount;
            string prefix = definition.IsNested
                ? FormatTypeName(definition.DeclaringType!, arguments, ownStart) + "."
                : string.IsNullOrEmpty(definition.Namespace) ? string.Empty : definition.Namespace + ".";
            string suffix = ownCount == 0
                ? string.Empty
                : "<" + string.Join(", ", arguments.Skip(ownStart).Take(ownCount).Select(FormatType)) + ">";
            return prefix + SimpleName(definition) + suffix;
        }

        private static string FormatConstant(object? value, Type declaredType)
        {
            if (value == null || value is DBNull || value is Missing)
            {
                return declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) == null ? "default" : "null";
            }

            switch (value)
            {
                case string text:
                    return "\"" + Escape(text) + "\"";
                case char character:
                    return "'" + Escape(character.ToString()) + "'";
                case bool flag:
                    return flag ? "true" : "false";
                case float single:
                    return single.ToString("R", CultureInfo.InvariantCulture) + "f";
                case double number:
                    return number.ToString("R", CultureInfo.InvariantCulture) + "d";
                default:
                    return FormatInvariant(value);
            }
        }

        private static string FormatInvariant(object? value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is Enum)
            {
                value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);
            }

            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }

        private static string Escape(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\'': builder.Append("\\'"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\0': builder.Append("\\0"); break;
                    default:
                        if (char.IsControl(character))
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
