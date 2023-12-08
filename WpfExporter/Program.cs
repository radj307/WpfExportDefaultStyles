using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;

namespace WpfExporter
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var argManager = new ArgManager(args,
                'o', "out", "output",
                'N', "namespace",
                'A', "assembly"
            );

            if (argManager.Args.Count == 0 || argManager.ContainsAny(ArgType.Flag | ArgType.Option, 'h', "help"))
            {
                var asm = Assembly.GetExecutingAssembly();
                Console.WriteLine($"{asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "WpfExporter"}{(asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is string version ? $" v{version}" : string.Empty)}{(asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright is string copyright ? $"  {copyright}" : string.Empty)}");
                Console.WriteLine("  Exports the default styles (including control templates) for the specified WPF control(s).");
                Console.WriteLine("  If an output file isn't specified, outputs to STDOUT. Status messages are disabled for STDOUT by default.");
                Console.WriteLine("  A regular expression can be specified in place of a typename by prepending it with \"regex:\".");
                Console.WriteLine();
                Console.WriteLine("USAGE:");
                Console.WriteLine($"  {Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName)} <OPTIONS> [[regex:]TYPENAME...]");
                Console.WriteLine($"  {Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName)} <OPTIONS> --all-resources");
                Console.WriteLine();
                Console.WriteLine("OPTIONS:");
                Console.WriteLine("  -h, --help                              Shows this help doc.");
                Console.WriteLine("  -q, --quiet                             Prevents messages from being written to the console. This is");
                Console.WriteLine("                                           implicitly specified if outputting to STDOUT instead of a file.");
                Console.WriteLine("      --include-messages                  Forces messages to be shown when outputting to STDOUT.");
                Console.WriteLine("  -o, --output <PATH>                     Specifies an output filepath. You can specify multiple arguments.");
                Console.WriteLine("  -O, --open                              Opens the output file(s) in the default program for the file type.");
                Console.WriteLine("  -i, --ignore-case                       Use case-insensitive string comparisons when searching for type names.");
                Console.WriteLine("  -N, --namespace <[regex:]NAMESPACE[+]>  Exports all styles in the specified namespace. Appending a '+' to");
                Console.WriteLine("                                           the namespace will include styles for types in sub-namespaces, too.");
                Console.WriteLine("                                           Specify regular expressions by prepending the value with \"regex:\".");
                Console.WriteLine("  -A, --assembly <[regex:]NAME>           Exports all styles in the specified assembly.");
                Console.WriteLine("                                           Specify regular expressions by prepending the value with \"regex:\".");
                Console.WriteLine("  -L, --load-assembly <ASSEMBLY>          Loads the specified assembly. Can be specified multiple times.");
                Console.WriteLine("                                           Accepts filepaths, directory paths, or assembly names.");
                return 0;
            }

            // create an application so that the default resources can be found
            Application app = new();

            var rc = Main_impl(argManager);

            app.Shutdown();
            return rc;
        }
        static int Main_impl(ArgManager args)
        {
            TextWriter? cout = Console.Out;
            TextWriter? cerr = Console.Error;

            try
            {
                var outPaths = args.GetAllValues(ArgType.Flag | ArgType.Option, 'o', "out", "output").ToArray();

                bool ignoreCase = args.ContainsAny(ArgType.Flag | ArgType.Option, 'i', "ignore-case");
                var stringComparison = ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                bool openOutputFiles = args.ContainsAny(ArgType.Flag | ArgType.Option, 'O', "open");

                if ((outPaths.Length == 0 && !args.ContainsAny(ArgType.Option, "include-messages")) || args.ContainsAny(ArgType.Flag | ArgType.Option, 'q', "quiet"))
                {
                    cout = null;
                    cerr = null;
                }

                var typeResolver = new TypeResolver(/* PresentationFramework assembly: */Assembly.GetAssembly(typeof(Control))!);

                // load additional assemblies
                foreach (var assemblyArg in args.GetAllValues(ArgType.Flag | ArgType.Option, 'L', "load-assembly"))
                {
                    if (Directory.Exists(assemblyArg))
                    { // directory
                        foreach (var file in Directory.EnumerateFiles(assemblyArg, "*.dll"))
                        {
                            try
                            {
                                var asm = Assembly.LoadFrom(file);
                                cout?.WriteLine($"Successfully loaded assembly \"{asm.FullName}\" from file \"{file}\" in directory \"{assemblyArg}\"");
                                typeResolver.PrependSearchAssemblyIfUnique(asm);
                            }
                            catch (Exception ex)
                            {
                                cerr?.WriteLine($"[ERROR]\tAn exception occurred while loading assembly from file \"{assemblyArg}\": {ex}");
                            }
                        }
                    }
                    else if (File.Exists(assemblyArg))
                    { // filepath
                        try
                        {
                            var asm = Assembly.LoadFrom(assemblyArg);
                            cout?.WriteLine($"Successfully loaded assembly \"{asm.FullName}\" from file \"{assemblyArg}\"");
                            typeResolver.PrependSearchAssemblyIfUnique(asm);
                        }
                        catch (Exception ex)
                        {
                            cerr?.WriteLine($"[ERROR]\tAn exception occurred while loading assembly from file \"{assemblyArg}\": {ex}");
                        }
                    }
                    else
                    { // raw
                        try
                        {
                            var asm = Assembly.Load(assemblyArg);
                            cout?.WriteLine($"Successfully loaded assembly \"{asm.FullName}\"");
                            typeResolver.PrependSearchAssemblyIfUnique(asm);
                        }
                        catch (Exception ex)
                        {
                            cerr?.WriteLine($"[ERROR]\tAn exception occurred while loading assembly from file \"{assemblyArg}\": {ex}");
                        }
                    }
                }

                var sb = new StringBuilder();
                using var xmlWriter = XmlWriter.Create(new StringWriter(sb), new()
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    ConformanceLevel = ConformanceLevel.Fragment,
                    NamespaceHandling = NamespaceHandling.OmitDuplicates
                });
                try
                {
                    List<Type> types = new();
                    // resolve typenames
                    {
                        var typeNameArgs = args.GetAll(ArgType.Parameter).Select(arg => arg.Name).ToArray();
                        var predicates = new List<Func<string, bool>>();
                        foreach (var arg in typeNameArgs)
                        {
                            // try resolving the type directly (handles fully qualified typenames):
                            if (Type.GetType(arg, throwOnError: false, ignoreCase) is Type type)
                            {
                                types.Add(type);
                                cout?.WriteLine($"Successfully resolved type \"{arg}\" => \"{type}\"");
                                continue;
                            }

                            if (arg.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
                            {
                                Regex rgx = new(arg[6..], RegexOptions.Compiled);
                                predicates.Add(typeName => rgx.IsMatch(typeName));
                            }
                            else
                            {
                                predicates.Add(typeName => typeName.Equals(arg, StringComparison.OrdinalIgnoreCase));
                            }
                        }

                        types.AddRange(typeResolver.ResolveAllTypesByName(typeName => predicates.Any(pred => pred(typeName))));
                    }

                    // add types from specified namespaces (if any)
                    {
                        var namespaceArgs = args
                            .GetAllValues(ArgType.Flag | ArgType.Option, 'N', "namespace")
                            .Select(arg =>
                            {
                                bool isRecursive = arg.EndsWith('+');
                                return ((string Namespace, bool Recurse))(isRecursive ? arg[..^1] : arg, isRecursive);
                            })
                            .ToArray();
                        var predicates = new List<Func<string, bool>>();
                        foreach (var (arg, recurse) in namespaceArgs)
                        {
                            if (arg.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
                            {
                                Regex rgx = new(arg[6..], RegexOptions.Compiled);
                                predicates.Add(@namespace => rgx.IsMatch(@namespace));
                            }
                            else
                            {
                                predicates.Add(@namespace => recurse
                                    ? @namespace.StartsWith(arg, stringComparison)
                                    : @namespace.Equals(arg, stringComparison));
                            }
                        }
                        if (namespaceArgs.Length > 0)
                        { // get ALL subclasses of FrameworkElement or FrameworkContentElement in ALL loaded assemblies
                            types.AddRange(typeResolver.ResolveAllNamespaceSubclassesOf(namespacePredicate: @namespace => !string.IsNullOrWhiteSpace(@namespace) && predicates.Any(pred => pred(@namespace)),
                                typeof(FrameworkElement), typeof(FrameworkContentElement)));
                        }
                    }

                    // add types from specified assemblies (if any)
                    {
                        var assemblyNameArgs = args.GetAllValues(ArgType.Flag | ArgType.Option, 'A', "assembly").ToArray();
                        var predicates = new List<Func<AssemblyName, bool>>();
                        foreach (var arg in assemblyNameArgs)
                        {
                            if (arg.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
                            {
                                Regex rgx = new(arg[6..], RegexOptions.Compiled);
                                predicates.Add(assemblyName => rgx.IsMatch(assemblyName.FullName) || (assemblyName.Name != null && rgx.IsMatch(assemblyName.Name)));
                            }
                            else
                            {
                                predicates.Add(assemblyName => assemblyName.FullName.Equals(arg, stringComparison) || (assemblyName.Name != null && assemblyName.Name.Equals(arg, stringComparison)));
                            }
                        }
                        if (assemblyNameArgs.Length > 0)
                        {
                            types.AddRange(AppDomain.CurrentDomain
                                .GetAssemblies()
                                .Where(asm => predicates.Any(pred => pred(asm.GetName())))
                                .SelectMany(asm => new TypeResolver(asm).ResolveAllSubclassesOf(typeof(FrameworkElement), typeof(FrameworkContentElement))));
                        }
                    }

                    if (types.Count == 0)
                        throw new InvalidOperationException("Nothing to export.");

                    // export templates for types
                    foreach (var type in types)
                    {
                        try
                        {
                            if (Application.Current.TryFindResource(type) is object resource)
                            {
                                xmlWriter.WriteComment($"  {type.AssemblyQualifiedName}  ");
                                XamlWriter.Save(resource, xmlWriter);
                                xmlWriter.Flush();
                                cout?.WriteLine($"Successfully retrieved template for type \"{type}\"");
                            }
                            else
                            {
                                cout?.WriteLine($"[ERROR]\tFailed to retrieve template for type \"{type}\"");
                            }
                        }
                        catch (Exception ex)
                        {
                            cerr?.WriteLine($"[ERROR]\tFailed to export type \"{type.FullName}\" due to an exception:\n{ex}");
                        }
                    }
                }
                finally
                { // output
                    string content = sb.ToString();
                    if (content.Length > 0)
                    {
                        if (outPaths.Length > 0)
                        { // output to files
                            foreach (var path in outPaths)
                            {
                                try
                                {
                                    File.WriteAllText(path, content);
                                    cout?.WriteLine($"Successfully saved \"{path}\"");

                                    if (openOutputFiles)
                                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    cerr?.WriteLine($"An exception occurred while attempting to write to \"{path}\":\n{ex}");
                                }
                            }
                        }
                        else // output to STDOUT 
                            (cout ?? Console.Out).WriteLine(content); //< override quiet for this line only
                    }
                    else cerr?.WriteLine("Nothing to write.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                cerr?.WriteLine($"[FATAL]\tThe program exited due to an exception:\n{ex}");
                return 1;
            }
        }
    }
}