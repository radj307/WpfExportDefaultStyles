# WPF Exporter
[![latest version](https://img.shields.io/github/v/tag/radj307/WpfExporter?filter=!mkrel-&style=flat-square&logo=github&label=Version)](https://github.com/radj307/WpfExporter/releases/latest)

Lightweight CLI utility for dumping the default Style & ControlTemplate of any WPF control.  

Requires [**.NET 6**](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) or newer.

## Usage

You can view the full help doc with the `-h` or `--help` arguments:  
```
> WpfExporter --help
WpfExporter v2.1.0  Copyleft 2023-2023 by radj307
  Exports the default styles (including control templates) for the specified WPF control(s).
  If an output file isn't specified, outputs to STDOUT. Status messages are disabled for STDOUT by default.
  A regular expression can be specified in place of a typename by prepending it with "regex:".

USAGE:
  WpfExporter <OPTIONS> [[regex:]TYPENAME...]
  WpfExporter <OPTIONS> --all-resources

OPTIONS:
  -h, --help                              Shows this help doc.
  -q, --quiet                             Prevents messages from being written to the console. This is
                                           implicitly specified if outputting to STDOUT instead of a file.
      --include-messages                  Forces messages to be shown when outputting to STDOUT.
  -o, --output <PATH>                     Specifies an output filepath. You can specify multiple arguments.
  -O, --open                              Opens the output file(s) in the default program for the file type.
  -i, --ignore-case                       Use case-insensitive string comparisons when searching for type names.
  -N, --namespace <[regex:]NAMESPACE[+]>  Exports all styles in the specified namespace. Appending a '+' to
                                           the namespace will include styles for types in sub-namespaces, too.
                                           Specify regular expressions by prepending the value with "regex:".
  -A, --assembly <[regex:]NAME>           Exports all styles in the specified assembly.
                                           Specify regular expressions by prepending the value with "regex:".
  -L, --load-assembly <ASSEMBLY>          Loads the specified assembly. Can be specified multiple times.
                                           Accepts filepaths, directory paths, or assembly names.
```

### Examples

#### Export to Clipboard
To copy the default style for a `Button` to your clipboard, you can use:  
```ps1
WpfExporter Button | clip
```
Then you can paste it anywhere with `Ctrl+V`.

#### Regex Matches
This will export the styles for all types with names ending in `Button`:  
```ps1
WpfExporter regex:.*Button
```
