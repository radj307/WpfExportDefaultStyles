using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;

namespace WpfExporter
{
    internal static class Program
    {
        static Assembly[] LoadedAssemblies => _loadedAssemblies ??= AppDomain.CurrentDomain.GetAssemblies();
        static Assembly[]? _loadedAssemblies = null;

        static Assembly PresentationFrameworkAssembly => _presentationFrameworkAssembly ??= Assembly.GetAssembly(typeof(Control))!;
        static Assembly? _presentationFrameworkAssembly = null;

        [STAThread]
        static int Main(string[] args)
        {
            var argManager = new ArgManager(args,
                'o', "out", "output"
            );

            if (argManager.Args.Count == 0 || argManager.ContainsAny(ArgType.Flag | ArgType.Option, 'h', "help"))
            {
                var asm = Assembly.GetExecutingAssembly();
                Console.WriteLine($"{asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "WpfExporter"}{(asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is string version ? $" v{version}" : string.Empty)}{(asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright is string copyright ? $"  {copyright}" : string.Empty)}");
                Console.WriteLine("  Exports the default styles (including control templates) for the specified WPF control(s).");
                Console.WriteLine("  If an output file isn't specified, outputs to STDOUT.");
                Console.WriteLine();
                Console.WriteLine("USAGE:");
                Console.WriteLine($"  {Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName)} <OPTIONS> [TYPENAME...]");
                Console.WriteLine();
                Console.WriteLine("OPTIONS:");
                Console.WriteLine("  -h, --help              Shows this help doc.");
                Console.WriteLine("  -q, --quiet             Prevents messages from being written to the console. This is");
                Console.WriteLine("                           implicitly specified if outputting to STDOUT instead of a file.");
                Console.WriteLine("      --include-messages  Forces messages to be shown when outputting to STDOUT.");
                Console.WriteLine("  -o, --output <PATH>     Specifies an output filepath. You can specify multiple arguments.");
                Console.WriteLine("  -O, --open              Opens the output file(s) in the default program.");
                Console.WriteLine("  -i, --ignore-case       Use case-insensitive string comparisons when searching for type names.");
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

                var sb = new StringBuilder();
                using var xmlWriter = XmlWriter.Create(new StringWriter(sb), new()
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    ConformanceLevel = ConformanceLevel.Fragment
                });
                try
                {
                    // resolve the type names to actual types
                    List<Type> types = new();
                    foreach (var typeName in args.GetAll(ArgType.Parameter).Select(arg => arg.Name))
                    {
                        try
                        {
                            if (Type.GetType(typeName, throwOnError: false, ignoreCase) is Type type)
                            {
                                types.Add(type);
                                cout?.WriteLine($"Successfully resolved type \"{typeName}\" => \"{type}\"");
                            }
                            else if (ResolveTypeFromName(typeName, stringComparison) is Type resolvedType)
                            {
                                types.Add(resolvedType);
                                cout?.WriteLine($"Successfully resolved type \"{typeName}\" => \"{resolvedType}\"");
                            }
                            else cerr?.WriteLine($"[ERROR]\tFailed to resolve typename \"{typeName}\"! (Is the namespace correct?)");
                        }
                        catch (Exception ex)
                        {
                            cerr?.WriteLine($"[ERROR]\tFailed to resolve typename \"{typeName}\" due to {ex.GetType().Name}:\n{ex}");
                        }
                    }

                    // export templates for types
                    foreach (var type in types)
                    {
                        try
                        {
                            if (ExportDefaultControlTemplates(type, out object template))
                            {
                                xmlWriter.WriteComment($"  {type.AssemblyQualifiedName}  ");
                                XamlWriter.Save(template, xmlWriter);
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
                        {
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
                        else (cout ?? Console.Out).WriteLine(content); //< override quiet for this line only
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

        static Type? ResolveTypeFromName(string typeName, StringComparison stringComparison)
        {
            if (PresentationFrameworkAssembly.DefinedTypes.FirstOrDefault(ti => ti.Name.Equals(typeName, stringComparison))
                is Type t) return t;
            // else fallback to searching all loaded assemblies
            foreach (var assembly in LoadedAssemblies)
            {
                if (assembly == PresentationFrameworkAssembly) continue; //< don't search this twice

                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Name.Equals(typeName, stringComparison))
                        return type;
                }
            }
            return null;
        }

        static bool ExportDefaultControlTemplates(Type type, out object template)
        {
            if (Application.Current.TryFindResource(type) is object resource)
            {
                template = resource;
                return true;
            }
            template = null!;
            return false;
        }
    }
}