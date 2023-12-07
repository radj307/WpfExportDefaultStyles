# WPF Exporter

Lightweight CLI utility for dumping the default styles of WPF controls.  

Requires [**.NET 6**](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) or newer.

## Usage

You can view the full help doc with the `-h` or `--help` arguments:  
```
> ./WpfExporter --help
WpfExporter v1.0.0  Copyleft 2023-2023 by radj307
  Exports the default styles (including control templates) for the specified WPF control(s).
  If an output file isn't specified, outputs to STDOUT.

USAGE:
  WpfExporter <OPTIONS> [TYPENAME...]

OPTIONS:
  -h, --help              Shows this help doc.
  -q, --quiet             Prevents messages from being written to the console. Implicitly included when no output path was specified.
      --include-messages  Forces messages to be shown when outputting to STDOUT.
  -o, --output <PATH>     Specifies an output filepath. You can specify multiple arguments.
  -O, --open              Opens the output file(s) in the default program.
  -i, --ignore-case       Use case-insensitive string comparisons when searching for type names.
```

### Examples

To copy the default style for a `Button` to your clipboard, you can use:  
```ps1
./WpfExporter Button | clip
```
Then you can paste it anywhere with `Ctrl+V`.
