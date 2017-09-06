using AVRDisassembler;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AVRControlFlowGraphGenerator
{
    public class BasicBlock : List<AssemblyStatement>
    {
        public Guid Guid { get; } = Guid.NewGuid();
        public int FirstOffset => this.First().Offset;
        public int LastOffset => this.Last().Offset;
        public string Name => $"Block_{Guid}";

        public override string ToString()
        {
            return 
                $"BB {Guid.ToString().Substring(0, 6)}...: " +
                $"@{FirstOffset:X8}->@{LastOffset:X8}" + 
                $"# of statements: {Count})";
        }
    }
}
