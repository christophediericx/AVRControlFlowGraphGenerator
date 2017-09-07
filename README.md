# AVRControlFlowGraphGenerator
An simple ATMEL AVR / Arduino Control Flow Graph Generator (for Intel HEX files).

A blog post with more information [can be found here](http://www.diericx.net/post/generating-avr-assembly-control-flow-graphs/).

![AVRControlFlowGraphGenerator](https://github.com/christophediericx/AVRControlFlowGraphGenerator/blob/master/Images/AVRControlFlowGraphGenerator.png)

## Command Line Usage ##

Run the program from the command line (without arguments) in order to display the built-in help:

```
2017-09-06 00:29:18.0345|INFO|AVRCFGGen|Parsing Command Line Arguments...
AVRControlFlowGraphGenerator 1.0.0
Christophe Diericx

ERROR(S):
  Required option 'i, input' is missing.
  Required option 'o, output' is missing.

  -i, --input           Required. Input file (.HEX) for which to generate a CFG.

  -o, --output          Required. Output file (.DOT).

  -s, --startaddress    (Default: 0) Start Offset (defaults to 0x0).

  -d, --dpi             (Default: 200) DPI.

  --help                Display this help screen.

  --version             Display version information.
```

`Input` and `Output` (file names) are the only required parameters.
