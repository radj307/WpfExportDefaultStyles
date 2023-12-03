using System.Diagnostics.CodeAnalysis;

namespace WpfControlTemplateExporter
{
    public class DynamicString
    {
        #region Constructors
        public DynamicString(char c) => Value = new(c, 1);
        public DynamicString(string s) => Value = s;
        #endregion Constructors

        #region Properties
        public string Value { get; set; }
        [DisallowNull]
        public char? CharValue
        {
            get => Value.Length == 1 ? Value[0] : null;
            set => Value = new(value.Value, 1);
        }
        #endregion Properties

        #region Conversion Operators
        public static implicit operator string(DynamicString s) => s.Value;
        public static implicit operator DynamicString(string s) => new(s);
        public static implicit operator DynamicString(char c) => new(c);
        #endregion Conversion Operators

        #region Methods
        public override string ToString() => Value;
        public bool Equals(DynamicString other, StringComparison stringComparison) => Value.Equals(other.Value, stringComparison);
        public bool Equals(DynamicString other) => Equals(other, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is DynamicString dynString && Equals(dynString);
        public override int GetHashCode() => Value.GetHashCode();
        #endregion Methods
    }
    public static class DynamicStringEnumerableExtensions
    {
        public static bool Contains(this IEnumerable<DynamicString> source, DynamicString value, StringComparison stringComparison = StringComparison.Ordinal)
            => source.Any(item => item.Equals(value, stringComparison));
    }

    public class ArgManager
    {
        #region Constructors
        public ArgManager(string[] args, params DynamicString[] implicitlyCapturingArgNames)
        {
            _args = Parse(args, implicitlyCapturingArgNames);
        }
        #endregion Constructors

        #region Properties
        public IReadOnlyList<IArg> Args => _args;
        private readonly List<IArg> _args;
        #endregion Properties

        #region Methods

        #region Parse
        public static List<IArg> Parse(string[] args, params DynamicString[] implicitlyCapturingArgNames)
        {
            List<IArg> l = new();

            for (int i = 0, i_max = args.Length; i < i_max; ++i)
            {
                var argName = args[i];

                if (argName.StartsWith('-'))
                { // non-parameter
                    argName = argName[1..];

                    // get capture arg if appended (don't need to check capturing args)
                    string? capture = null;
                    var eqPos = argName.IndexOf('=');
                    if (eqPos != -1)
                    {
                        capture = argName[(eqPos + 1)..];
                        argName = argName[..eqPos];
                    }

                    if (argName.StartsWith('-'))
                    { // option
                        argName = argName[1..];

                        if (capture == null && implicitlyCapturingArgNames.Contains(argName) && i + 1 < i_max && !args[i + 1].StartsWith('-'))
                            capture = args[++i];

                        l.Add(new CaptureArg(ArgType.Option, argName, capture));
                    }
                    else
                    { // flag
                        var j_last = argName.Length - 1;
                        // handle all chained flags except for the first one
                        if (argName.Length > 1)
                        {
                            for (int j = 0; j < j_last; ++j)
                            {
                                l.Add(new CaptureArg(ArgType.Flag, new string(argName[j], 1)));
                            }
                        }
                        // handle the last flag in the chain
                        var flagName = new string(argName[j_last], 1);

                        if (capture == null && implicitlyCapturingArgNames.Contains(flagName) && i + 1 < i_max && !args[i + 1].StartsWith('-'))
                            capture = args[++i];

                        l.Add(new CaptureArg(ArgType.Flag, flagName, capture));
                    }
                }
                else
                { // parameter
                    l.Add(new Arg(ArgType.Parameter, argName));
                }
            }

            return l;
        }
        #endregion Parse

        #region GetAny
        public IArg? GetAny(ArgType types, params DynamicString[] names)
            => Args.FirstOrDefault(arg => types.HasFlag(arg.Type) && names.Contains(arg.Name));
        public IArg? GetAny(params DynamicString[] names)
            => Args.FirstOrDefault(arg => names.Contains(arg.Name));
        #endregion GetAny

        #region GetAll
        public IEnumerable<IArg> GetAll(ArgType types)
            => Args.Where(arg => types.HasFlag(arg.Type));
        public IEnumerable<IArg> GetAll(ArgType types, params DynamicString[] names)
            => Args.Where(arg => types.HasFlag(arg.Type) && names.Contains(arg.Name));
        public IEnumerable<IArg> GetAll(params DynamicString[] names)
            => Args.Where(arg => names.Contains(arg.Name));
        #endregion GetAll

        #region (Private) ArgToValue
        private static string? ArgToValue(IArg? arg)
            => arg == null ? null : arg is ICaptureArg captureArg ? captureArg.Value : arg.Name;
        #endregion (Private) ArgToValue

        #region GetAnyValue
        public string? GetAnyValue(ArgType types, params DynamicString[] names)
            => ArgToValue(GetAny(types, names));
        public string? GetAnyValue(params DynamicString[] names)
            => ArgToValue(GetAny(names));
        #endregion GetAnyValue

        #region GetAllValues
        public IEnumerable<string> GetAllValues(ArgType types, Func<string?, bool> predicate)
            => GetAll(types).Select(arg => arg is ICaptureArg captureArg ? captureArg.Value : arg.Name).Where(predicate).ToArray()!;
        public IEnumerable<string> GetAllValues(ArgType types, params DynamicString[] names)
            => GetAll(types, names).Select(arg => ArgToValue(arg)).Where(arg => arg != null)!;
        public IEnumerable<string> GetAllValues(params DynamicString[] names)
            => GetAll(names).Select(arg => ArgToValue(arg)).Where(arg => arg != null)!;
        #endregion GetAllValues

        #region ContainsAny
        public bool ContainsAny(ArgType types, params DynamicString[] names)
            => Args.Any(arg => types.HasFlag(arg.Type) && names.Contains(arg.Name));
        public bool ContainsAny(params DynamicString[] names)
            => Args.Any(arg => names.Contains(arg.Name));
        #endregion ContainsAny

        #region ContainsAll
        public bool ContainsAll(ArgType types, params DynamicString[] names)
        {
            List<string> remainingNames = new(names.Select(name => name.ToString()));

            foreach (var arg in Args)
            {
                if (!types.HasFlag(arg.Type)) continue;

                if (remainingNames.Contains(arg.Name))
                    remainingNames.Remove(arg.Name);
            }

            return remainingNames.Count == 0;
        }
        public bool ContainsAll(params DynamicString[] names)
        {
            List<string> remainingNames = new(names.Select(name => name.ToString()));

            foreach (var arg in Args)
            {
                if (remainingNames.Contains(arg.Name))
                    remainingNames.Remove(arg.Name);
            }

            return remainingNames.Count == 0;
        }
        #endregion ContainsAll

        #endregion Methods

        #region (class) Arg
        class Arg : IArg
        {
            public Arg(ArgType type, string name)
            {
                Type = type;
                Name = name;
            }

            public ArgType Type { get; }
            public string Name { get; }

            public override string ToString() => Name;
        }
        class CaptureArg : Arg, ICaptureArg
        {
            public CaptureArg(ArgType type, string name, string? value = null) : base(type, name)
            {
                Value = value;
            }

            public string? Value { get; }
        }
        #endregion (class) Arg
    }

    [Flags]
    public enum ArgType : byte
    {
        None = 0,
        Parameter = 1,
        Flag = 2,
        Option = 4,
    }
    public interface IArg
    {
        ArgType Type { get; }
        string Name { get; }
    }
    public interface ICaptureArg : IArg
    {
        string? Value { get; }
    }
}