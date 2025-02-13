// See https://aka.ms/new-console-template for more information
using System.Runtime.CompilerServices;
using System.Text;
using MGQParadoxUtilities;

const string CommonEventDirParam = "-CommonEventsDir:";
const string CommonEventDirParamShort = "-c:";
const string HLibraryParam = "-HLibraryParam:";
const string HLibraryParamShort = "-h:";
const string PicturesDirParam = "-PicturesDir:";

const string Usage = $"Usage: MGQParadoxUtilities.exe {CommonEventDirParam}|{CommonEventDirParamShort}< CommonEvents directory path> {HLibraryParam}|{HLibraryParamShort}< Full path to the '202 - Library(H).rb' file> -verbose|-v -overwrite|-o \n" +
    "Command line app that will take the existing temptation or seduction CommonEvent2xxx files and copy and reformat them to CommonEvent9XXX files in the same CommonEvent directory and add them to the H scene gallery so they can be viewed anytime.\n" +
    "-verbose will print additional diagnotic messages, -overwrite will enable overriting existing CommonEvent9XXX files (if this tool has been previously run), -skipHLibrary will skip updating the '202 - Library(H).rb' file";

string commonEventDir = string.Empty;
string hLibraryFile = string.Empty;
string picturesDir = string.Empty;
bool overwriteExistingScenes = false;
bool skipHLibraryUpdate = false;
bool testHLibaraySerialization = false;
bool verbose = false;

DateTime start = DateTime.Now;

foreach (var arg in args)
{
    if (arg.StartsWith(CommonEventDirParam, StringComparison.OrdinalIgnoreCase))
    {
        if (!String.IsNullOrEmpty(commonEventDir))
        {
            Console.WriteLine(Usage);
            throw new ArgumentException("CommonEvents directory already specified", CommonEventDirParam);
        }
        commonEventDir = arg.Substring(CommonEventDirParam.Length).Trim().Trim('"');
    }
    else if (arg.StartsWith(CommonEventDirParamShort, StringComparison.OrdinalIgnoreCase))
    {
        if (!String.IsNullOrEmpty(commonEventDir))
        {
            Console.WriteLine(Usage);
            throw new ArgumentException("CommonEvents directory already specified", CommonEventDirParamShort);
        }
        commonEventDir = arg.Substring(CommonEventDirParamShort.Length).Trim().Trim('"');
    }
    else if (arg.StartsWith(HLibraryParam, StringComparison.OrdinalIgnoreCase))
    {
        if (!String.IsNullOrEmpty(hLibraryFile))
        {
            Console.WriteLine(Usage);
            throw new ArgumentException("H scene gallery file (202 - Library(H).rb) already specified", HLibraryParam);
        }
        hLibraryFile = arg.Substring(HLibraryParam.Length).Trim().Trim('"');
    }
    else if (arg.StartsWith(HLibraryParamShort, StringComparison.OrdinalIgnoreCase))
    {
        if (!String.IsNullOrEmpty(hLibraryFile))
        {
            Console.WriteLine(Usage);
            throw new ArgumentException("H scene gallery file (202 - Library(H).rb) already specified", HLibraryParamShort);
        }
        hLibraryFile = arg.Substring(HLibraryParamShort.Length).Trim().Trim('"');
    }
    else if (arg.StartsWith(PicturesDirParam, StringComparison.OrdinalIgnoreCase))
    {
        picturesDir = arg.Substring(PicturesDirParam.Length).Trim().Trim('"');
    }
    else if (arg.StartsWith("-v", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-verbose", StringComparison.OrdinalIgnoreCase))
    {
        verbose = true;
    }
    else if (arg.StartsWith("-o", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-overwrite", StringComparison.OrdinalIgnoreCase))
    {
        overwriteExistingScenes = true;
    }
    else if (arg.StartsWith("-skipHLibrary", StringComparison.OrdinalIgnoreCase))
    {
        skipHLibraryUpdate = true;
    }
    else if (arg.StartsWith("-testHLibrary", StringComparison.OrdinalIgnoreCase))
    {
        testHLibaraySerialization = true;
    }
    else
    {
        Console.WriteLine(Usage);
        throw new ArgumentException("Unknown argument: '" + arg + "'");
    }
}

if (!Directory.Exists(commonEventDir))
{
    Console.WriteLine(Usage);
    throw new DirectoryNotFoundException("CommonEvent directory not found: " + commonEventDir);
}
if (!File.Exists(hLibraryFile))
{
    Console.WriteLine(Usage);
    throw new DirectoryNotFoundException("HLibrary file not found: " + hLibraryFile);
}

if (testHLibaraySerialization)
{
    Console.WriteLine("Testing serialization and deserialization of HLIbrary on :" + hLibraryFile);
    HGallery hGallery = new HGallery(hLibraryFile, new List<TemptSceneOutput>(), verbose);
    hGallery.TestDeSerializationAndSerialization();
    return 0;
}

// These are some special files that should not be processed.
// Most of them are cases where it's a group monster with a bunch of copy and pasted tempt scripts for each one but only one entry in the h scene gallery
HashSet<string> commonEventExceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    // specail event for team reactions when Luka sucums to a temptation scene.
    "CommonEvent2000.txt",
    // there are 3 identical zombie twin scenes but only one entry in the h scene gallery
    "CommonEvent2079.txt",
    "CommonEvent2080.txt",
    "CommonEvent2081.txt",
    // there are 6 identical fairy scenes but only one entry in the h scene gallery
    "CommonEvent2088.txt",
    "CommonEvent2089.txt",
    "CommonEvent2090.txt",
    "CommonEvent2091.txt",
    "CommonEvent2092.txt",
    // each member of the group of Ittan-Momen have a nearly identical temp scene but just one entry in the h scene gallery
    "CommonEvent2159.txt",
    "CommonEvent2160.txt",
    // ignore extra duplicate Ghouls
    "CommonEvent2242.txt",
    "CommonEvent2243.txt",
    // ignore duplicate succubus harm scenarios
    "CommonEvent2269.txt",
    "CommonEvent2270.txt",
    "CommonEvent2271.txt",
    // ignore duplicate Arachnes
    "CommonEvent2322.txt",
    "CommonEvent2323.txt",
    // ignore duplicate Angel Soldiers
    "CommonEvent2354.txt",
    "CommonEvent2355.txt",
    "CommonEvent2356.txt",
    "CommonEvent2357.txt",
    // ignore duplicate trinity members
    "CommonEvent2359.txt",
    "CommonEvent2360.txt",
    // ignore duplicate Kunoichi Elves
    "CommonEvent2516.txt",
    "CommonEvent2517.txt",
    // ignore duplicate black Dahlia scenario
    "CommonEvent2531.txt",
    // ignore duplicate Black Mamba scenario
    "CommonEvent2532.txt",
    // ignore duplicate Black Rose scenario
    "CommonEvent2533.txt",
    // ignore duplicate Erubetie scenario
    "CommonEvent2535.txt",
    // ignore duplicate Zion scenario
    "CommonEvent2538.txt",
    // ignore duplicate Gnosis scenario
    "CommonEvent2539.txt",
    // ignore duplicate null_kenzoku
    "CommonEvent2957.txt",
    "CommonEvent2958.txt",
};

List<string> temptCommonEventFiles = Directory.EnumerateFiles(commonEventDir, "CommonEvent2*.txt").Where((temptFile) =>
{
    string fileName = Path.GetFileName(temptFile);
    // we only want the CommonEvent2001.txt files with 4 digits, also skip the first CommonEvent2000.txt file.
    return fileName.Length >= 19 && !commonEventExceptions.Contains(fileName);
}).ToList();

if (verbose)
{
    Console.WriteLine($"Found {temptCommonEventFiles.Count} CommonEvent2xxx.txt tempt files in '{commonEventDir}'");
    Console.WriteLine("Tempt scene conversion starting...");
}

int startSnapshot = TemptSceneConverter.OutputEventCounter;
List<TemptSceneOutput> convertedScenes = new List<TemptSceneOutput>();
foreach (string temptFile in temptCommonEventFiles)
{
    try
    {
        TemptSceneConverter converter = new TemptSceneConverter(temptFile, picturesDir, verbose, overwriteExistingScenes);
        TemptSceneOutput output = converter.CreateHSceneEvent();
        if (verbose)
        {
            if (output == null)
            {
                Console.WriteLine($"Skipped '{temptFile}'");
            }
            else
            {
                Console.WriteLine($"Processed '{temptFile}' found defeat id: {output.DefeatIndex}, output file: '{output.OutputTemptFileName}'");
            }
        }
        if (output != null)
        {
            convertedScenes.Add(output);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error encountered processing file: " + temptFile);
        Console.WriteLine(ex.ToString());
    }
}
Console.WriteLine($"Finished creating new scenes from 'CommonEvent{startSnapshot}.txt' to 'CommonEvent{TemptSceneConverter.OutputEventCounter}.txt'");

if (skipHLibraryUpdate)
{
    Console.WriteLine("Skipping updating the H-scene library file.");
}
else
{
    if (verbose)
    {
        Console.WriteLine($"Starting to update the '{hLibraryFile}' file.");
    }
    HGallery hGallery = new HGallery(hLibraryFile, convertedScenes, verbose);
    hGallery.StartUpdate();
}

TimeSpan duration = DateTime.Now - start;
Console.WriteLine($"Done, time elapsed: {duration}");

return 0;
