using AVRDisassembler;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpCodes = AVRDisassembler.InstructionSet.OpCodes;

namespace AVRControlFlowGraphGenerator
{
    public class AVRControlFlowGraphGenerator
    {
        internal const string LoggerName = "AVRCFGGen";
        private static readonly Logger Logger = LogManager.GetLogger(LoggerName);
        private AVRControlFlowGraphGeneratorOptions Options { get; }
        private AssemblyStatement[] Disassembly { get; set; }
        private int HighestOffset { get; set; }
        private IDictionary<int,AssemblyStatement> AddressLookup { get; set; }
        private IList<BasicBlock> BasicBlocks { get; }
        private HashSet<Relation> Relations { get; }
        private HashSet<int> VisitedEntryPoints { get; }
        private Stack<int> CallStack { get; }

        public AVRControlFlowGraphGenerator(AVRControlFlowGraphGeneratorOptions options)
        {
            Options = options;
            AddressLookup = new Dictionary<int, AssemblyStatement>();
            BasicBlocks = new List<BasicBlock>();
            Relations = new HashSet<Relation>();
            VisitedEntryPoints = new HashSet<int>();
            CallStack = new Stack<int>(1024);
        }

        public void GenerateCFG()
        {
            Logger.Info("Generating CFG...");

            // Disassemble
            Logger.Info($"Disassembling '{Options.InputFile}'...");
            Disassembly = Disassemble(Options.InputFile);
            Logger.Trace(
                string.Join(
                    Environment.NewLine, 
                    new []{ "Disassembly:" }.Concat(Disassembly.Select(x => x.ToString()))
                ));

            // Store highest address
            HighestOffset = Disassembly.Last().Offset;
            Logger.Debug($"Highest Offset: {HighestOffset}.");

            // Create direct address lookup table
            Logger.Debug("Creating address lookup table...");
            AddressLookup = CreateAddressLookupTable(Disassembly);

            // First pass: traverse the tree recursively (depth first)
            Logger.Info("First pass (traversing the tree)...");
            foreach (var block in ParseBlock(Options.StartAddress))
            {
                Logger.Debug($"Adding <{block}>");
                BasicBlocks.Add(block);
            }

            // Second pass: find blocks that overlap, and split them
            Logger.Info($"Number of Basic Blocks found: {BasicBlocks.Count()}.");
            var relationsToRemove = new List<Relation>();
            var relationsToAdd = new List<Relation>();
            foreach (var block in BasicBlocks)
            {
                var blockStart = block.FirstOffset;
                var blockEnd = block.LastOffset;
                foreach (var otherBlock in BasicBlocks.Where(x => x.Guid != block.Guid))
                {
                    var otherBlockStart = otherBlock.FirstOffset;
                    var otherBlockEnd = otherBlock.LastOffset;
                    var overlap = blockStart < otherBlockEnd && otherBlockStart < blockEnd;
                    if (!overlap) continue;
                    Logger.Trace($"Overlapping blocks found: {block}-{otherBlock}.");

                    /* 
                         *  |                  +------------+
                         *  |                  | otherBlock |
                         *  |   +------------+ |            |
                         *  |   | block      | |            |
                         *  |   +------------| +------------+
                         * \|/
                         *  '
                         * or
                         * 
                         *  |   +------------+
                         *  |   | block      | 
                         *  |   |            | +------------+
                         *  |   |            | | otherBlock |
                         *  |   +------------| +------------+
                         * \|/
                         *  '
                         */
                    if (blockEnd == otherBlockEnd && blockStart != otherBlockStart)
                    {
                        var biggestBlock = blockStart > otherBlockStart ? otherBlock : block;
                        var smallestBlock = blockStart > otherBlockStart ? block : otherBlock;

                        // Shrink the biggest block ...
                        biggestBlock.RemoveAll(x => x.Offset >= smallestBlock.FirstOffset);

                        // Move relations of biggest block ...
                        foreach (var otherRelation in Relations.Where(x => x.Source == biggestBlock.FirstOffset))
                        {
                            relationsToRemove.Add(otherRelation);
                            relationsToAdd.Add(new Relation(smallestBlock.FirstOffset, otherRelation.Target));
                        }
                        // ... and add a new relation
                        relationsToAdd.Add(new Relation(biggestBlock.FirstOffset, smallestBlock.FirstOffset));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    // Remove relations
                    foreach (var relationToRemove in relationsToRemove)
                        Relations.Remove(relationToRemove);

                    // Add relations
                    foreach (var relationToAdd in relationsToAdd)
                        if (!Relations.Contains(relationToAdd))
                            Relations.Add(relationToAdd);
                }
            }

            // Generate graph
            var graphBuilder = new StringBuilder();
            graphBuilder.AppendLine("digraph G {");
            graphBuilder.AppendLine($"    graph[dpi={Options.DPI},bgcolor=\"#333333\",fontcolor=\"white\"];");
            graphBuilder.AppendLine("    node[style=filled,shape=box,fontcolor=\"white\",color =\"white\",fillcolor=\"#006699\",fontname=\"Consolas\"];");
            graphBuilder.AppendLine("    edge[style=dashed,color=\"white\",arrowhead=open];");

            foreach (var block in BasicBlocks)
                graphBuilder.AppendLine(
                    $"    \"{FormatBlock(block.FirstOffset)}\" "
                    + $"[label=\"{FormatContents(block.ToArray())}\"];");

            foreach (var relation in Relations)
                graphBuilder.AppendLine(FormatRelation(relation));

            graphBuilder.AppendLine("}");
            var graph = graphBuilder.ToString();
            File.WriteAllText(Options.OutputFile, graph);
        }

        #region Private Methods

        private static AssemblyStatement[] Disassemble(string file)
        {
            var disAssembler = new Disassembler(
                new DisassemblerOptions
                {
                    File = file, JsonOutput = false
                });
            return disAssembler.Disassemble().ToArray();
        }

        private static IDictionary<int,AssemblyStatement> CreateAddressLookupTable(IEnumerable<AssemblyStatement> statements)
        {
            return statements.ToDictionary(stmt => stmt.Offset);
        }

        private AssemblyStatement FindEntry(int offset)
        {
            AddressLookup.TryGetValue(offset, out var result);
            if (result == null) throw new InvalidOperationException($"Unable to find statement at offset {offset:X8}!");
            return result;
        }

        private IEnumerable<BasicBlock> ParseBlock(int startOffset)
        {
            var stmt = FindEntry(startOffset);
            var currentBlock = new BasicBlock();
            Logger.Trace($"Visiting {startOffset}..");
            VisitedEntryPoints.Add(startOffset);

            while (true)
            {
                currentBlock.Add(stmt);
                if (IsBranch(stmt))
                {
                    yield return currentBlock;

                    var relationsToAdd = new List<Relation>();
                    Logger.Debug($"Encountered {stmt.OpCode.Name}: {stmt}");

                    // Unconditional Jumps
                    if (stmt.OpCode is OpCodes.Branch.JMP)
                    {
                        relationsToAdd.Add(new Relation(startOffset, stmt.Operands.ToArray()[0].Value));
                    }
                    else if (stmt.OpCode is OpCodes.Branch.RJMP)
                    {
                        var op = stmt.Operands.ToArray()[0];
                        relationsToAdd.Add(new Relation(startOffset, stmt.Offset + op.Value + 2));
                    }
                    // Conditional Jumps
                    else if (stmt.OpCode is OpCodes.Branch.BRBC || stmt.OpCode is OpCodes.Branch.BRBS)
                    {
                        var op = stmt.Operands.ToArray()[1];
                        relationsToAdd.Add(new Relation(startOffset, stmt.Offset + op.Value + 2));
                        relationsToAdd.Add(new Relation(startOffset, stmt.Offset + 2));
                    }
                    // Call And Return
                    else if (stmt.OpCode is OpCodes.Branch.CALL)
                    {
                        relationsToAdd.Add(new Relation(startOffset, stmt.Operands.ToArray()[0].Value));
                        CallStack.Push(stmt.Offset + 4);
                    }
                    // Return
                    else if (stmt.OpCode is OpCodes.Branch.RET)
                    {
                        var returnTarget = CallStack.Pop();
                        relationsToAdd.Add(new Relation(startOffset, returnTarget));
                    }

                    foreach (var relation in relationsToAdd)
                    {
                        var source = relation.Source;
                        var target = relation.Target;
                        Logger.Trace($"Adding relation {source} -> {target}");
                        if (!Relations.Contains(relation)) Relations.Add(relation);
                        if (VisitedEntryPoints.Contains(target)) continue;
                        foreach (var block in ParseBlock(target))
                            yield return block;
                    }
                    break;
                }
                var nextSequentialInstruction = stmt.Offset + stmt.OriginalBytes.Count();
                if (nextSequentialInstruction > HighestOffset) break;
                stmt = FindEntry(nextSequentialInstruction);
            }
        }

        private static bool IsBranch(AssemblyStatement stmt)
        {
            return stmt.OpCode is OpCodes.Branch.JMP || stmt.OpCode is OpCodes.Branch.RJMP
                || stmt.OpCode is OpCodes.Branch.BRBC || stmt.OpCode is OpCodes.Branch.BRBS
                || stmt.OpCode is OpCodes.Branch.CALL || stmt.OpCode is OpCodes.Branch.RET;
        }

        private static string FormatRelation(Relation relation)
        {
            return $"    \"{FormatBlock(relation.Source)}\" -> \"{FormatBlock(relation.Target)}\";";
        }

        private static string FormatBlock(int startOffset)
        {
            return $"Block_{startOffset:X8}";
        }

        private static string FormatContents(IEnumerable<AssemblyStatement> statements)
        {
            return string.Join(@"\l", 
                statements.Select(x => x.ToString()).Concat(new[] { string.Empty }));
        }

        #endregion
    }
}
