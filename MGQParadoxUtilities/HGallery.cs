using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// This is a silly idea, but I'm going to try deserializing the ruby file to an in memory model and then re-serializing it.

namespace MGQParadoxUtilities
{
    public class HGallery
    {
        private readonly string hLibraryFile;
        private readonly List<TemptSceneOutput> convertedScenes;
        private readonly bool verbose;
        private string[] preContent;
        private string[] hSceneConent;

        public HGallery(string hLibraryFile, List<TemptSceneOutput> convertedScenes, bool verbose)
        {
            this.hLibraryFile = hLibraryFile;
            this.convertedScenes = convertedScenes;
            this.verbose = verbose;
        }

        public void StartUpdate()
        {
            string backupFile = this.hLibraryFile + ".bak";
            if (!File.Exists(backupFile))
            {
                Console.WriteLine($"Backing up '{this.hLibraryFile}' to '{backupFile}'");
                File.Copy(this.hLibraryFile, backupFile);
            }

            HSceneEntries hSceneEntries = this.LoadFile();

            // I'm using the CommonEvent defeat id linked to in every tempt scene to identify which character entry in the H scene library each tempt scene should be added to.
            // Create a lookup table by CommonEventId to make this easier.

            Dictionary<int, List<CharacterEntry>> lookupTable = hSceneEntries.GetCharEntryByCommonEventIdLookupTable();
            foreach (TemptSceneOutput temptScene in this.convertedScenes)
            {
                List<CharacterEntry> characterEntries;
                if (lookupTable.TryGetValue(temptScene.DefeatIndex, out characterEntries))
                {
                    if (characterEntries.Count == 1)
                    {
                        HScene newScene = null;
                        CharacterEntry characterEntry = characterEntries[0];

                        if (characterEntry.hScenes.Any((scene) => scene.CommonEvent == temptScene.TemptIndex))
                        {
                            if (verbose)
                            {
                                Console.WriteLine($"Tempt scene {temptScene.TemptIndex} has already been added to character {characterEntry.Name}, skipping.");
                            }
                            continue;
                        }

                        for (int i = 0; i < characterEntry.hScenes.Count; i++)
                        {
                            HScene hScene = characterEntry.hScenes[i];
                            if (hScene.CommonEvent == temptScene.DefeatIndex)
                            {
                                newScene = new HScene(hScene);
                                newScene.CommonEvent = temptScene.TemptIndex;
                                string defeatPostfix = hScene.Name.Substring(6);
                                newScene.Name = "Seduce" + defeatPostfix;
                                characterEntry.hScenes.Insert(i + 1, newScene);
                                if (verbose)
                                {
                                    Console.WriteLine($"Added new scene {newScene.Name} to {characterEntry.Name}");
                                }
                                break;
                            }
                        }
                        if (newScene == null)
                        {
                            Console.WriteLine($"Error, failed to add new tempt scene {temptScene.TemptIndex} to character {characterEntry.Name}");
                        }
                    }
                    else if (characterEntries.Count > 1)
                    {
                        string matchingCharacters = String.Join(',', characterEntries.Select(e => e.Name).ToArray());
                        Console.WriteLine($"Found multiple character entries {matchingCharacters} with Defeat CommonEvent: {temptScene.DefeatIndex}, not sure where to add: {temptScene.TemptIndex}");
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to find a character entry with Defeat CommonEvent: {temptScene.DefeatIndex} to add tempt scene {temptScene.TemptIndex}");
                }
            }

            Console.WriteLine($"Overwriting {this.hLibraryFile} with new scene entries.");
            using (StreamWriter sw = new StreamWriter(this.hLibraryFile))
            {
                List<string> serializedLines = new List<string>();
                serializedLines.AddRange(this.preContent);
                serializedLines.AddRange(hSceneEntries.Serialize());
                foreach (string line in serializedLines)
                {
                    sw.WriteLine(line);
                }
            }
        }

        public void TestDeSerializationAndSerialization()
        {
            HSceneEntries hSceneEntries = this.LoadFile();
            string testFile = this.hLibraryFile + ".test";
            Console.WriteLine("Writing re-serialized H-Library file to: " + testFile);
            using (StreamWriter sw = new StreamWriter(testFile))
            {
                List<string> serializedLines = new List<string>();
                serializedLines.AddRange(this.preContent);
                serializedLines.AddRange(hSceneEntries.Serialize());
                foreach (string line in serializedLines)
                {
                    sw.WriteLine(line);
                }
            }
        }

        // I'm taking a hybrid approach to this. I'm just going to string copy the content I don't want to modify like the H_SCENE_MEMORY_BG_IMAGE section, and I'm going to load and and re-serialize the H_SCENE_ITEMS section.
        // I feel like modifying the H_SCENE_ITEMS is too complicated and error prone to try to do it by directly modifying the text.
        // The proper way to do this would be to create (or find) an ANTLR grammar and generate the parser and AST tree, but that ended up being more trouble than it was worth.
        // "It's a bold strategy Cotton. Let's see if it pays off for him."
        public HSceneEntries LoadFile()
        {
            var preContentTemp = new List<string>();
            var hSceneConentTemp = new List<string>();
            HSceneEntries hSceneEntries = new HSceneEntries();
            bool haveSeenHSceneItems = false;

            using (StreamReader sr = new StreamReader(this.hLibraryFile))
            {
                string? line = sr.ReadLine();
                while (line != null)
                {
                    if (!haveSeenHSceneItems)
                    {
                        preContentTemp.Add(line);
                    }
                    else
                    {
                        hSceneConentTemp.Add(line);
                    }

                    if (line.Contains("H_SCENE_ITEMS"))
                    {
                        haveSeenHSceneItems = true;
                    }

                    line = sr.ReadLine();
                }
            }
            this.preContent = preContentTemp.ToArray();
            this.hSceneConent = hSceneConentTemp.ToArray();

            hSceneEntries.Load(this.hSceneConent, this.verbose);
            return hSceneEntries;
        }
    }

    public static class CommonParsingUtilities
    {
        public readonly static Regex EntryStartRegex = new Regex("(\\d+)\\s*=>\\s*{");
        public readonly static Regex NameRegex = new Regex(":name\\s*=>\\s*\"([^\"]+)\"");
        public readonly static Regex CommonRegex = new Regex(":common\\s*=>\\s*(\\d+)");
        public readonly static Regex TypeRegex = new Regex(":type\\s*=>\\s*(\\d+)");
        public readonly static Regex OpRegex = new Regex(":op\\s*=>\\s*(\\d+)");
        public readonly static Regex ValueRegex = new Regex(":value\\s*=>\\s*(\\d+)");
        public readonly static Regex IdNumberRegex = new Regex(":id\\s*=>\\s*(\\d+)");
        public readonly static Regex IdAnyRegex = new Regex(":id\\s*=>\\s*([^ ,]+)");
        public readonly static Regex FadeoutRegex = new Regex(":fadeout\\s*=>\\s*(\\d+)");
        public readonly static Regex MapRegex = new Regex("map:\\s*\\s*([^\\]]+\\])");
    }

    public class HSceneEntries
    {
        private readonly List<string> leadingComments = new List<string>();
        private readonly List<CharacterEntry> entries = new List<CharacterEntry>();
        private readonly List<string> postContent = new List<string>();

        public List<CharacterEntry> Entries
        {
            get { return entries; }
        }

        public int Load(string[] hSceneConent, bool verbose)
        {
            int lineNum = 0;
            while (lineNum < hSceneConent.Length)
            {
                string line = hSceneConent[lineNum];
                Match entryStartMatch = CommonParsingUtilities.EntryStartRegex.Match(line);
                if (entryStartMatch.Success)
                {
                    int id = int.Parse(entryStartMatch.Groups[1].Value);
                    if (verbose)
                    {
                        Console.WriteLine("Loading HScene entry: " + id);
                    }
                    var characterEntry = new CharacterEntry(id);
                    entries.Add(characterEntry);
                    lineNum = characterEntry.Load(hSceneConent, lineNum + 1, verbose);
                }
                else if (line.Trim().StartsWith("}"))
                {
                    lineNum++;
                    break;
                }
                else
                {
                    Console.WriteLine($"HSceneEntries parser did not find a character entry start on this line: '{line}'");
                    lineNum++;
                }
            }
            while (lineNum < hSceneConent.Length)
            {
                postContent.Add(hSceneConent[lineNum]);
                lineNum++;
            }
            return lineNum;
        }

        public List<string> Serialize()
        {
            List<string> serialized = new List<string>()
            {
                "    # ID"
            };
            for (int i = 0; i < this.entries.Count; i++)
            {
                CharacterEntry entry = this.entries[i];
                serialized.AddRange(entry.Serialize());
                if ((i + 1) < this.entries.Count)
                {
                    serialized[serialized.Count - 1] += ',';
                }
            }
            serialized.Add("  }");
            serialized.AddRange(this.postContent);
            return serialized;
        }

        public Dictionary<int, List<CharacterEntry>> GetCharEntryByCommonEventIdLookupTable()
        {
            // Unfortunately I think it is possible for multiple characters to share the same defeat id.
            Dictionary<int, List<CharacterEntry>> lookupTable = new Dictionary<int, List<CharacterEntry>>();
            foreach (CharacterEntry entry in this.entries)
            {
                foreach (HScene scene in entry.hScenes)
                {
                    List<CharacterEntry> characterEntries;
                    if (!lookupTable.TryGetValue(scene.CommonEvent, out characterEntries))
                    {
                        characterEntries = new List<CharacterEntry>();
                        lookupTable[scene.CommonEvent] = characterEntries;
                    }
                    characterEntries.Add(entry);
                }
            }
            return lookupTable;
        }
    }

    public class CharacterEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string[] UnParsedConditionAndCg { get; set; }
        public List<HScene> hScenes { get; set; }

        public CharacterEntry(int id)
        {
            this.Id = id;
        }

        public int Load(string[] hSceneConent, int lineNum, bool verbose)
        {
            this.hScenes = new List<HScene>();
            bool hasParsedName = false;
            bool hasSeenItems = false;
            List<string> unParsedConditionAndCgTemp = new List<string>();
            int closingBracketsSeen = 0;
            while (lineNum < hSceneConent.Length)
            {
                string line = hSceneConent[lineNum];
                if (!hasParsedName)
                {
                    Match nameMatch = CommonParsingUtilities.NameRegex.Match(line);
                    if (nameMatch.Success)
                    {
                        this.Name = nameMatch.Groups[1].Value;
                        hasParsedName = true;
                        if (verbose)
                        {
                            Console.WriteLine("\tLoading character: " + this.Name);
                        }
                    }
                }
                else if (!hasSeenItems)
                {
                    if (line.Contains(":items"))
                    {
                        hasSeenItems = true;
                    }
                    else
                    {
                        unParsedConditionAndCgTemp.Add(line);
                    }
                }
                else if (line.Trim().StartsWith("}"))
                {
                    closingBracketsSeen++;
                    // Need to see 2 both for the end of the items section and for the end of the overall character section.
                    if (closingBracketsSeen >= 2)
                    {
                        lineNum++;
                        break;
                    }
                }
                else
                {
                    Match entryStartMatch = CommonParsingUtilities.EntryStartRegex.Match(line);
                    if (entryStartMatch.Success)
                    {
                        int id = int.Parse(entryStartMatch.Groups[1].Value);
                        if (verbose)
                        {
                            Console.WriteLine("\tLoading HScene item: " + id);
                        }
                        var hSceneItem = new HScene();
                        this.hScenes.Add(hSceneItem);
                        lineNum = hSceneItem.Load(hSceneConent, lineNum + 1, verbose);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"HSceneCharacterEntry parser did not find a item entry start on this line: '{line}'");
                    }
                }
                lineNum++;
            }
            this.UnParsedConditionAndCg = unParsedConditionAndCgTemp.ToArray();
            return lineNum;
        }

        public List<string> Serialize()
        {
            List<string> serialized = new List<string>()
            {
                $"    {this.Id} => {{",
                $"      :name => \"{this.Name}\","
            };
            serialized.AddRange(this.UnParsedConditionAndCg);
            serialized.Add("      :items => {");
            for (int i = 0; i < this.hScenes.Count; i++)
            {
                HScene scene = this.hScenes[i];
                serialized.Add($"        {i + 1} => {{");
                serialized.AddRange(scene.Serialize());
                if ((i + 1) < this.hScenes.Count)
                {
                    serialized[serialized.Count - 1] += ',';
                }
            }
            serialized.Add("      }");
            serialized.Add("    }");
            return serialized;
        }
    }

    public class HScene
    {
        public string Name { get; set; }
        public int CommonEvent { get; set; }
        public int? Fadeout { get; set; }
        public HSceneCondition Condition { get; set; }
        public HSceneGGBackground Background { get; set; }
        public string Map { get; set; }

        public HScene() { }
        public HScene(HScene other)
        {
            Name = other.Name;
            CommonEvent = other.CommonEvent;
            Fadeout = other.Fadeout;
            Condition = other.Condition;
            Background = other.Background;
            Map = other.Map;
        }

        public int Load(string[] hSceneConent, int lineNum, bool verbose)
        {
            while (lineNum < hSceneConent.Length)
            {
                string line = hSceneConent[lineNum];
                string trimmedLine = line.Trim();
                Match nameMatch = CommonParsingUtilities.NameRegex.Match(line);
                Match commonMatch = CommonParsingUtilities.CommonRegex.Match(line);
                if (nameMatch.Success)
                {
                    this.Name = nameMatch.Groups[1].Value;
                }
                else if (commonMatch.Success)
                {
                    this.CommonEvent = int.Parse(commonMatch.Groups[1].Value);
                }
                else if (trimmedLine.StartsWith(":condition"))
                {
                    this.Condition = new HSceneCondition();
                    lineNum = this.Condition.Load(hSceneConent, lineNum + 1, verbose);
                    continue;
                }
                else if (trimmedLine.StartsWith(":background"))
                {
                    this.Background = new HSceneGGBackground();
                    lineNum = this.Background.Load(hSceneConent, lineNum + 1, verbose);
                    continue;
                }
                else if (trimmedLine.StartsWith("}"))
                {
                    lineNum++;
                    break;
                }
                else
                {
                    // some edge case values.
                    Match mapMatch = CommonParsingUtilities.MapRegex.Match(line);
                    Match fadeoutMatch = CommonParsingUtilities.FadeoutRegex.Match(line);
                    if (mapMatch.Success)
                    {
                        this.Map = mapMatch.Groups[1].Value;
                    }
                    else if (fadeoutMatch.Success)
                    {
                        this.Fadeout = int.Parse(fadeoutMatch.Groups[1].Value);
                    }
                    else
                    {
                        Console.WriteLine($"HSceneItem:Unexpected line encountered: '{line}'");
                    }
                }
                lineNum++;
            }
            return lineNum;
        }

        public List<string> Serialize()
        {
            List<string> serialized = new List<string>()
            {
                $"          :name => \"{this.Name}\",",
                $"          :common => {this.CommonEvent}"
            };
            if (this.Fadeout.HasValue)
            {
                serialized[serialized.Count - 1] += ',';
                serialized.Add("          :fadeout => " + this.Fadeout);
            }
            if (this.Condition != null)
            {
                serialized[serialized.Count - 1] += ',';
                serialized.AddRange(this.Condition.Serialize());
            }
            if (this.Background != null)
            {
                serialized[serialized.Count - 1] += ',';
                serialized.AddRange(this.Background.Serialize());
            }
            if (!String.IsNullOrEmpty(this.Map))
            {
                serialized[serialized.Count - 1] += ',';
                serialized.Add("          map: " + this.Map);
            }
            serialized.Add("        }");
            return serialized;
        }
    }

    public class HSceneCondition
    {
        public int? Type { get; set; }
        public string Id { get; set; }
        public int? Op { get; set; }
        public int? Value { get; set; }

        public int Load(string[] hSceneConent, int lineNum, bool verbose)
        {
            while (lineNum < hSceneConent.Length)
            {
                string line = hSceneConent[lineNum];
                Match typeMatch = CommonParsingUtilities.TypeRegex.Match(line);
                Match idMatch = CommonParsingUtilities.IdAnyRegex.Match(line);
                Match opMatch = CommonParsingUtilities.OpRegex.Match(line);
                Match valueMatch = CommonParsingUtilities.ValueRegex.Match(line);
                if (typeMatch.Success)
                {
                    this.Type = int.Parse(typeMatch.Groups[1].Value);
                }
                else if (idMatch.Success)
                {
                    this.Id = idMatch.Groups[1].Value;
                }
                else if (opMatch.Success)
                {
                    this.Op = int.Parse(opMatch.Groups[1].Value);
                }
                else if (valueMatch.Success)
                {
                    this.Value = int.Parse(valueMatch.Groups[1].Value);
                }
                else if (line.Trim().StartsWith("}"))
                {
                    lineNum++;
                    break;
                }
                else
                {
                    Console.WriteLine($"HSceneCondition:Unexpected line encountered: '{line}'");
                }
                lineNum++;
            }
            return lineNum;
        }

        public List<string> Serialize()
        {
            List<string> serialized = new List<string>();
            serialized.Add("          :condition => {");
            if (this.Type.HasValue)
            {
                serialized.Add("            :type => " + this.Type);
            }
            if (!String.IsNullOrEmpty(this.Id))
            {
                serialized[serialized.Count - 1] += ',';
                serialized.Add("            :id => " + this.Id);
            }
            if (this.Op.HasValue)
            {
                serialized[serialized.Count - 1] += ',';
                serialized.Add("            :op => " + this.Op);
            }
            if (this.Value.HasValue)
            {
                serialized[serialized.Count - 1] += ',';
                serialized.Add("            :value => " + this.Value);
            }
            serialized.Add("          }");
            return serialized;
        }
    }

    public class HSceneGGBackground
    {
        public string Id { get; set; }

        public int Load(string[] hSceneConent, int lineNum, bool verbose)
        {
            while (lineNum < hSceneConent.Length)
            {
                string line = hSceneConent[lineNum];
                Match idMatch = CommonParsingUtilities.IdAnyRegex.Match(line);
                if (idMatch.Success)
                {
                    this.Id = idMatch.Groups[1].Value;
                }
                else if (line.Trim().StartsWith("}"))
                {
                    lineNum++;
                    break;
                }
                else
                {
                    Console.WriteLine($"HSceneGGBackground:Unexpected line encountered: '{line}'");
                }
                lineNum++;
            }
            return lineNum;
        }

        public List<string> Serialize()
        {
            List<string> serialized = new List<string>()
            {
                "          :background => {",
               $"            :id => {this.Id}",
                "          }"
            };
            return serialized;
        }
    }
}
