using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using OpenDreamShared.Dream;
using OpenDreamShared.Dream.Procs;
using OpenDreamShared.Json;

namespace DMDisassembler {
    class Program {
        public static DreamCompiledJson CompiledJson;
        public static DMProc GlobalInitProc = null;
        public static List<DMProc> Procs = null;
        public static Dictionary<DreamPath, DMType> AllTypes = null;

        private static readonly string NoTypeSelectedMessage = "No type is selected";

        private static DMType _selectedType = null;

        static void Main(string[] args) {
            if (args.Length == 0 || Path.GetExtension(args[0]) != ".json") {
                Console.WriteLine("The json output of DMCompiler must be provided as an argument");

                return;
            }

            string compiledJsonText = File.ReadAllText(args[0]);

            CompiledJson = JsonSerializer.Deserialize<DreamCompiledJson>(compiledJsonText);
            if (CompiledJson.GlobalInitProc != null) GlobalInitProc = new DMProc(CompiledJson.GlobalInitProc);
            LoadAllProcs();
            LoadAllTypes();

            bool acceptingCommands = true;
            while (acceptingCommands) {
                if (_selectedType != null) {
                    Console.Write(_selectedType.Path);
                }
                Console.Write("> ");

                string input = Console.ReadLine();
                string[] split = input.Split(" ");
                string command = split[0].ToLower();

                switch (command) {
                    case "q": acceptingCommands = false; break;
                    case "search": Search(split); break;
                    case "sel":
                    case "select": Select(split); break;
                    case "list": List(split); break;
                    case "d":
                    case "decompile": Decompile(split); break;
                    case "find_opcode_clusters": FindOpcodeClusters(split); break;
                    default: Console.WriteLine("Invalid command \"" + command + "\""); break;
                }
            }
        }

        private static void Search(string[] args) {
            if (args.Length < 3) {
                Console.WriteLine("search type|proc [name]");

                return;
            }

            string type = args[1];
            string name = args[2];
            if (type == "type") {
                foreach (DreamPath typePath in AllTypes.Keys) {
                    if (typePath.PathString.Contains(name)) Console.WriteLine(typePath);
                }
            } else if (type == "proc") {
                if (_selectedType == null) {
                    Console.WriteLine(NoTypeSelectedMessage);

                    return;
                }

                foreach (string procName in _selectedType.Procs.Keys) {
                    if (procName.Contains(name)) Console.WriteLine(procName);
                }
            } else {
                Console.WriteLine("Invalid search type \"" + type + "\"");
            }
        }

        private static void Select(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("select [type]");

                return;
            }

            string type = args[1];
            if (AllTypes.TryGetValue(new DreamPath(type), out DMType dmType)) {
                _selectedType = dmType;
            } else {
                Console.WriteLine("Invalid type \"" + type + "\"");
            }
        }

        private static void List(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("list procs|children");

                return;
            }

            if (_selectedType == null) {
                Console.WriteLine(NoTypeSelectedMessage);

                return;
            }

            string what = args[1];
            if (what == "procs") {
                foreach (string procName in _selectedType.Procs.Keys) {
                    Console.WriteLine(procName);
                }
            } else if (what == "children") {
                foreach(var typeTuple in AllTypes) {
                    if(typeTuple.Key.IsDescendantOf(_selectedType.Path)) {
                        Console.WriteLine(typeTuple.Key.PathString);
                    }
                }
            } else {
                Console.WriteLine("list procs|children");
                return;
            }
        }

        private static void Decompile(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("decompile [name]");

                return;
            }

            string name = args[1];
            if (name == "__global_init__") {
                if (GlobalInitProc != null) {
                    Console.WriteLine(GlobalInitProc.Decompile());
                } else {
                    Console.WriteLine("There is no global init proc");
                }

                return;
            }

            if (_selectedType == null) {
                Console.WriteLine(NoTypeSelectedMessage);
                return;
            }

            if (name == "__init__") {
                if (_selectedType.InitProc != null) {
                    Console.WriteLine(_selectedType.InitProc.Decompile() + "\n");
                } else {
                    Console.WriteLine("Selected type does not have an init proc");
                }
            } else if (_selectedType.Procs.TryGetValue(name, out DMProc proc)) {
                Console.WriteLine(proc.Decompile() + "\n");
            } else {
                Console.WriteLine("No procs named \"" + name + "\"");
            }
        }

        private static void FindOpcodeClusters(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("find_opcode_clusters [size]");
                return;
            }
            if (!int.TryParse(args[1], out int depth) || depth < 2) {
                Console.WriteLine("find_opcode_clusters argument must be an integer value greater than one");
                return;
            }

            List<OpcodeCluster> best = new List<OpcodeCluster>(4096);
            int i = 0;
            foreach(DMProc proc in Procs) {
                i += 1;
                if (proc.Bytecode.Length == 0)
                    continue;
                try {
                    proc.PollOpcodes(ref best, depth);
                } catch(System.IO.EndOfStreamException) {

                } catch(System.ArgumentOutOfRangeException) {

                }
                best.Sort();
                if(best.Count > 2048)
                    best.RemoveRange(2048, best.Count- 2048);
                if ((i & 8192) == 8192)
                    Console.WriteLine($"{(float)i / Procs.Count * 100f:n3}%...");
            }
            foreach (OpcodeCluster cluster in best) {
                Console.WriteLine(cluster);
            }
        }

        private static void LoadAllProcs() {
            Procs = new List<DMProc>(CompiledJson.Procs.Length);

            foreach (ProcDefinitionJson procDef in CompiledJson.Procs) {
                Procs.Add(new DMProc(procDef));
            }
        }

        /// <remarks> Needs to be not be called before LoadAllProcs(). </remarks>
        private static void LoadAllTypes() {
            AllTypes = new Dictionary<DreamPath, DMType>(CompiledJson.Types.Length);

            foreach (DreamTypeJson json in CompiledJson.Types) {
                AllTypes.Add(new DreamPath(json.Path), new DMType(json));
            }

            //Add global procs to the root type
            DMType globalType = AllTypes[DreamPath.Root];
            foreach (int procId in CompiledJson.GlobalProcs) {
                var proc = Procs[procId];

                globalType.Procs.Add(proc.Name, proc);
            }
        }
    }

    struct OpcodeCluster : IComparable<OpcodeCluster> {
        public DreamProcOpcode[] codes;
        /// <summary>Stores the number of times we've seen this batch of opcodes together, so far.</summary>
        public int pollCount;

        public int CompareTo(OpcodeCluster other) {
            return other.pollCount.CompareTo(this.pollCount); // Inverted comparison so that more popular codes are sorted to the front.
        }

        public override string ToString() {
            StringBuilder ret = new StringBuilder($"{pollCount}: {{");
            ret.AppendJoin(',', codes);
            ret.Append('}');
            return ret.ToString();
        }
    }
}
