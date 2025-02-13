using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MGQParadoxUtilities
{
    /// <summary>
    /// Purpose: takes a CommonEvent2XXX tempt scene and converts it to a CommonEvent9XXX scene for use in the H-Scene gallery
    /// </summary>
    public class TemptSceneConverter
    {
        public static int OutputEventCounter = 9200;
        public static readonly Regex DefeatRegex = new Regex(@"lose_event_id\s*=\s*(\d\d\d\d)\s*[""]");
        public static readonly Regex PictureNameRegex = new Regex(@"\[\s*\d\s*,\s*\""([^\""]+)\""\s*,");
        private readonly string temptEventFilePath;
        private readonly string picturesDirectory;
        private readonly bool verbose;
        private readonly bool overwriteExistingScenes;
        private bool hasSeenFirstShowChoices = false;
        private bool inPreable = true;
        private string defeatIndex = null;
        private int lastShowPictureIndexOriginal = -1;
        private int lastShowPictureIndexOutput = -1;

        private Dictionary<string, string> specialCaseDefeatIdMapping = new Dictionary<string, string>()
        {
            { "3169", "3168" },
            // need to note that theres only one entry for the 4 slimes, but the 3227 defeat is referenced, not the 3228 defeat. Going to be a mess for mapping to the h scene later :-(
            { "3229", "3228" },
            { "3230", "3228" },
            // this appears to be a bug with sabiriel
            { "3571", "3570" },
            // this appears to be a bug with moruboru
            { "3702", "3819" },
            // this appears to be a bug with asmodeus
            { "3722", "3821" },
            // this appears to be a bug with jormungand
            { "3723", "3822" },
            // this appears to be a bug with asyura
            { "3815", "3744" },
            // this appears to be a bug with null_kenzoku
            { "3957", "3947" },
        };

        public TemptSceneConverter(string temptEventFilePath, string picturesDirectory, bool verbose, bool overwriteExistingScenes)
        {
            this.temptEventFilePath = temptEventFilePath;
            this.picturesDirectory = picturesDirectory;
            this.verbose = verbose;
            this.overwriteExistingScenes = overwriteExistingScenes;
        }

        public TemptSceneOutput CreateHSceneEvent()
        {
            if (!File.Exists(temptEventFilePath))
            {
                throw new FileNotFoundException("input event not found", temptEventFilePath);
            }

            string[] lines = ReadFile();

            if (lines.Length <= 22)
            {
                // If the file is too small this is probably not a tempt scene
                // for example it's a common pattern that there are some files that just forward it to a different file.
                return null;
            }

            List<string> outputContent = this.ProcessFile(lines);

            string commonEventsDirectory = Path.GetDirectoryName(this.temptEventFilePath);
            string newEvent = Path.Combine(commonEventsDirectory, "CommonEvent" + TemptSceneConverter.OutputEventCounter.ToString() + ".txt");

            if (!this.overwriteExistingScenes && File.Exists(newEvent))
            {
                Console.WriteLine($"Skipped overwriting existing file '{newEvent}' from '{this.temptEventFilePath}'");
            }
            else
            {
                using (StreamWriter sw = new StreamWriter(newEvent, false))
                {
                    outputContent.ForEach((outputLine) =>
                    {
                        sw.WriteLine(outputLine);
                    });
                }
                if (verbose)
                {
                    Console.WriteLine($"Output file has {outputContent.Count} lines.");
                }
                Console.WriteLine($"Created '{newEvent}' from '{this.temptEventFilePath}'");
            }

            return new TemptSceneOutput(TemptSceneConverter.OutputEventCounter++, int.Parse(this.defeatIndex), newEvent);
        }

        public string[] ReadFile()
        {
            List<string> lines = new List<string>();
            using (StreamReader sr = new StreamReader(this.temptEventFilePath))
            {
                string? line = sr.ReadLine();
                while (line != null)
                {
                    lines.Add(line);
                    line = sr.ReadLine();
                }
            }
            if (this.verbose)
            {
                Console.WriteLine($"Found {lines.Count} in {this.temptEventFilePath}");
            }

            return lines.ToArray();
        }

        public List<string> ProcessFile(string[] lines)
        {
            List<string> outputContent = new List<string>();
            int lineNum = 0;
            while (lineNum < lines.Length)
            {
                lineNum = this.ProcessEventLine(lineNum, lines, outputContent);
            }

            if (this.inPreable)
            {
                throw new Exception("Finished Processing file still in preable mode.");
            }

            // Can only append extra images if the pictures directory is passed in.
            if (!String.IsNullOrEmpty(this.picturesDirectory) && Directory.Exists(this.picturesDirectory))
            {
                TryAddExtraPicture(lines, outputContent);
            }

            return outputContent;
        }

        private void TryAddExtraPicture(string[] lines, List<string> outputContent)
        {
            if (this.lastShowPictureIndexOriginal != -1)
            {
                // For some reason a lot of these tempt scenes have additional progression images but don't show them.
                // This is an experimental feature to try to show one additional progression image.
                NextPictureData nextImageData = this.GetNextImageName(this.lastShowPictureIndexOriginal, lines);
                if (nextImageData != null)
                {
                    bool haveSeenShowTextAttributes = false;
                    for (int pictLineNum = this.lastShowPictureIndexOutput; pictLineNum < outputContent.Count; pictLineNum++)
                    {
                        string pictLine = outputContent[pictLineNum].Trim();
                        if (pictLine.StartsWith("ShowTextAttributes", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!haveSeenShowTextAttributes)
                            {
                                haveSeenShowTextAttributes = true;
                            }
                            else
                            {
                                string lastShowPicture = lines[this.lastShowPictureIndexOriginal];
                                string extraShowPicture = lastShowPicture.Replace(nextImageData.OriginalPictureName, nextImageData.NextPictureName);
                                outputContent.Insert(this.lastShowPictureIndexOutput, extraShowPicture);
                            }
                        }
                    }
                }
            }
            else if (verbose)
            {
                Console.WriteLine("No ShowPicture event found.");
            }
        }

        public int ProcessEventLine(int lineNum, string[] lines, List<string> outputContent)
        {
            string line = lines[lineNum];
            string trimmedLine = line.Trim();
            if (lineNum == 0)
            {
                outputContent.Add("CommonEvent " + TemptSceneConverter.OutputEventCounter.ToString());
                return lineNum + 1;
            }
            else if (inPreable)
            {
                if (trimmedLine.StartsWith("999"))
                {
                    outputContent.Add($"  999([{TemptSceneConverter.OutputEventCounter.ToString()}])");
                    return lineNum + 1;
                }
                else if (trimmedLine.Contains("game_troop.lose_event_id", StringComparison.OrdinalIgnoreCase))
                {
                    Match defeatMatch = DefeatRegex.Match(line);
                    if (!defeatMatch.Success)
                    {
                        throw new Exception("Failed to find the defeat id on this line: '" + line + "'");
                    }
                    string defeatIndexTemp = defeatMatch.Groups[1].Value;
                    if (this.specialCaseDefeatIdMapping.ContainsKey(defeatIndexTemp))
                    {
                        defeatIndexTemp = this.specialCaseDefeatIdMapping[defeatIndexTemp];
                    }
                    this.defeatIndex = defeatIndexTemp;

                    int parsedDefeatIndex = int.Parse(defeatIndex);
                    if (parsedDefeatIndex < 3000)
                    {
                        throw new Exception("Invalid defeat index found on this line: '" + line + "'");
                    }

                    this.inPreable = false;

                    IEnumerable<string> defaultCharacterImage = this.FindDefaultCharacterImageFromDefeatFile(defeatIndex);
                    if (defaultCharacterImage != null && defaultCharacterImage.Count() > 0)
                    {
                        outputContent.AddRange(defaultCharacterImage);
                        if (verbose)
                        {
                            Console.WriteLine($"Found defeat index: {this.defeatIndex}, found default image: {defaultCharacterImage.FirstOrDefault()}");
                        }
                    }
                    else if (verbose)
                    {
                        Console.WriteLine($"Found defeat index: {this.defeatIndex}, default image not found.");
                    }
                    return lineNum + 1;
                }
            }
            else
            {
                if (trimmedLine.StartsWith("ControlVariables", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("ControlSwitches", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("ChangeHP", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("Script", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith(@"ShowText([""Luka takes ", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("CallCommonEvent([2000])", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip this line
                    return lineNum + 1;
                }
                else if (trimmedLine.StartsWith("ShowPicture(", StringComparison.OrdinalIgnoreCase))
                {
                    this.lastShowPictureIndexOriginal = lineNum;
                    this.lastShowPictureIndexOutput = outputContent.Count;
                }
                else if (trimmedLine.StartsWith("ShowChoices", StringComparison.OrdinalIgnoreCase))
                {
                    return this.HandleChoices(lineNum, lines, outputContent);
                }
                else if (trimmedLine.StartsWith("ConditionalBranch", StringComparison.OrdinalIgnoreCase))
                {
                    return this.HandleConditionalBranch(lineNum, lines);
                }
                else if (trimmedLine.StartsWith("Label(", StringComparison.OrdinalIgnoreCase))
                {
                    return this.HandleLabel(lineNum, lines);
                }
            }

            // Default behavior, pass through existing line.
            outputContent.Add(line);
            return lineNum + 1;
        }

        /// <summary>
        /// Gets the default character image from the defeat scene file.
        /// The first ShowPicture command in the defeat scene is (nearly?) always the correct character image to show at the start.
        /// Just scans through the defeat scene file and returns the first ShowPicture command
        /// </summary>
        public IEnumerable<string> FindDefaultCharacterImageFromDefeatFile(string defeatIndex)
        {
            string commonEventsDirectory = Path.GetDirectoryName(this.temptEventFilePath);
            string defeatFileName = "CommonEvent" + defeatIndex + ".txt";
            string defeatFilePath = Path.Combine(commonEventsDirectory, defeatFileName);

            if (File.Exists(defeatFilePath))
            {
                using (StreamReader sr = new StreamReader(defeatFilePath))
                {
                    // ShowPicture([5, "80_kamakiri_st02", 0, 0, 0, 0, 100, 100, 0, 0])
                    string? line = sr.ReadLine();
                    while (line != null)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("ShowPicture", StringComparison.OrdinalIgnoreCase))
                        {
                            List<string> pictureLines = new List<string>();
                            pictureLines.Add(line);

                            // The defeat scenes start with the image hidden and need the next MovePicture command there to make it visible.
                            string nextLine = sr.ReadLine();
                            string nextLineTrimmed = nextLine.Trim();
                            if (nextLineTrimmed.StartsWith("MovePicture", StringComparison.OrdinalIgnoreCase))
                            {
                                pictureLines.Add(nextLine);
                            }
                            else
                            {
                                // if the MovePicture command isn't there, then try to just modify the line to make it visible from the start.
                                pictureLines[0] = line.Replace("100, 100, 0, 0]", "100, 100, 255, 0]");
                            }
                            return pictureLines;
                        }
                        line = sr.ReadLine();
                    }
                }
            }

            return null;
        }

        public int HandleChoices(int lineNum, string[] lines, List<string> outputContent)
        {
            int choiceLineNum = lineNum;
            if (this.hasSeenFirstShowChoices == false)
            {
                // railroad player, skip past the first choice to participate in the tempt scene
                for (choiceLineNum = lineNum; choiceLineNum < lines.Length; choiceLineNum++)
                {
                    string choiceLine = lines[choiceLineNum].Trim();
                    if (choiceLine.StartsWith("ChoicesEnd", StringComparison.OrdinalIgnoreCase))
                    {
                        this.hasSeenFirstShowChoices = true;
                        // Just skip the first choice.
                        return choiceLineNum + 1;
                    }
                }
                throw new Exception("Never found the end of the first Choices block");
            }
            else
            {
                // only show choices where the choice doesn't matter (no early endings)
                List<WhenBlock> whenBlocks = new List<WhenBlock>();
                for (choiceLineNum = lineNum + 1; choiceLineNum < lines.Length; choiceLineNum++)
                {
                    string choiceLine = lines[choiceLineNum];
                    string trimmedLine = choiceLine.Trim();
                    if (String.IsNullOrEmpty(trimmedLine))
                    {
                        continue;
                    }
                    else if (trimmedLine.StartsWith("When", StringComparison.OrdinalIgnoreCase))
                    {
                        var whenBlock = new WhenBlock();
                        whenBlocks.Add(whenBlock);
                    }
                    else if (trimmedLine.StartsWith("ExitEventProcessing([])", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.StartsWith("CallCommonEvent([1999])", StringComparison.OrdinalIgnoreCase))
                    {
                        var whenBlock = whenBlocks.Last();
                        whenBlock.IsExitBlock = true;
                    }
                    else if (trimmedLine.StartsWith("ShowText", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.StartsWith("ShowPicture", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.StartsWith("PlaySE", StringComparison.OrdinalIgnoreCase))
                    {
                        var whenBlock = whenBlocks.Last();
                        whenBlock.lines.Add(choiceLine);
                    }
                    else if (trimmedLine.StartsWith("ChoicesEnd", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                if (choiceLineNum >= lines.Length)
                {
                    throw new Exception("Choice block processing error, never found end of choices block");
                }

                if (whenBlocks.Count <= 1)
                {
                    throw new Exception("Something went weird? Only found 1 when block in a choices block. Normally should just be multiple choices?");
                }

                if (whenBlocks.All(block => !block.IsExitBlock))
                {
                    // None of the paths exit, leave the whole block in
                    for (choiceLineNum = lineNum; choiceLineNum < lines.Length; choiceLineNum++)
                    {
                        string choiceLine = lines[choiceLineNum];
                        outputContent.Add(choiceLine);
                        string trimmedChoiceLine = choiceLine.Trim();
                        if (trimmedChoiceLine.StartsWith("ChoicesEnd", StringComparison.OrdinalIgnoreCase))
                        {
                            return choiceLineNum + 1;
                        }
                    }
                }
                else if (whenBlocks.Any(block => !block.IsExitBlock))
                {
                    WhenBlock whenBlock = whenBlocks.First(block => !block.IsExitBlock);
                    outputContent.AddRange(whenBlock.lines);
                    return choiceLineNum + 1;
                }
            }
            throw new Exception("Something went wrong in Choice block processing. Fell through to catchall exception block.");
            return choiceLineNum + 1;
        }

        public int HandleConditionalBranch(int lineNum, string[] lines)
        {
            // just skip ConditionalBranch sections, they are all just health checks that don't apply.
            for (int branchLineNum = lineNum; branchLineNum < lines.Length; branchLineNum++)
            {
                string choiceLine = lines[branchLineNum].Trim();
                if (choiceLine.StartsWith("BranchEnd", StringComparison.OrdinalIgnoreCase))
                {
                    return branchLineNum + 1;
                }
            }
            throw new Exception("Never found the end of the ConditionalBranch block");
        }

        public int HandleLabel(int lineNum, string[] lines)
        {
            int indexOfLastLabel = -1;
            // If there are multiple labels, this scene has multiple endings, first ones are premature ones, we want the last one.
            for (int labelLineNum = lineNum + 1; labelLineNum < lines.Length; labelLineNum++)
            {
                string labelLine = lines[labelLineNum].Trim();
                if (labelLine.StartsWith("Label(", StringComparison.OrdinalIgnoreCase))
                {
                    indexOfLastLabel = labelLineNum;
                }
            }

            if (indexOfLastLabel == -1)
            {
                // only one Label or ending, just go to next line.
                return lineNum + 1;
            }
            else
            {
                // multiple endings, skip to the last one.
                return indexOfLastLabel + 1;
            }
        }

        public NextPictureData GetNextImageName(int lastShowPictureIndex, string[] lines)
        {
            StringBuilder stringBuilder = new StringBuilder();
            try
            {
                string line = lines[lastShowPictureIndex].Trim();
                Match pictureNameMatch = PictureNameRegex.Match(line);
                if (!pictureNameMatch.Success)
                {
                    throw new Exception("Failed to find the picture name on this line: '" + line + "'");
                }
                string pictureName = pictureNameMatch.Groups[1].Value;
                stringBuilder.Append($"Last picture shown: {pictureName}");

                string lastChar = pictureName[pictureName.Length - 1].ToString();
                if (int.TryParse(lastChar, out int lastNum))
                {
                    string nextPictureName = pictureName.Substring(0, pictureName.Length - 1) + (lastNum + 1).ToString();
                    stringBuilder.Append($" estimated next picture {nextPictureName}");
                    string nextPicturePath = Path.Combine(this.picturesDirectory, nextPictureName + ".png");
                    if (File.Exists(nextPicturePath))
                    {
                        stringBuilder.Append($" exists");
                        // There's a lot of ways this may be the wrong thing to do
                        return new NextPictureData(pictureName, nextPictureName);
                    }
                    else
                    {
                        stringBuilder.Append($" does not exist.");
                    }
                }
                else
                {
                    stringBuilder.Append(" does not end in number.");
                }
                return null;
            }
            finally
            {
                if (this.verbose)
                {
                    Console.WriteLine(stringBuilder.ToString());
                }
            }
        }
    }

    public class WhenBlock
    {
        public bool IsExitBlock { get; set; }
        public List<String> lines { get; set; }

        public WhenBlock()
        {
            IsExitBlock = false;
            lines = new List<string>();
        }
    }

    public class NextPictureData
    {
        public string OriginalPictureName { get; set; }
        public string NextPictureName { get; set; }

        public NextPictureData(string originalPictureName, string nextPictureName)
        {
            OriginalPictureName = originalPictureName;
            NextPictureName = nextPictureName;
        }
    }

    public class TemptSceneOutput
    {
        public int TemptIndex { get; set; }
        public int DefeatIndex { get; set; }
        public string OutputTemptFileName { get; set; }

        public TemptSceneOutput(int temptIndex, int defeatIndex, string outputTemptFileName)
        {
            TemptIndex = temptIndex;
            DefeatIndex = defeatIndex;
            OutputTemptFileName = outputTemptFileName;
        }
    }
}
