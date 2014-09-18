using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace backupTool
{
    static class Program
    {
        static string source;
        static string destination;
        static string destinationCacheFile;
        static string logFile;

        static Dictionary<string, DateTime> sourceFiles;
        static Dictionary<string, DateTime> destinationFiles;
        static Dictionary<string, DateTime> copyFiles;


        //flags
        static bool sourceFilesDone = false;
        static bool destinationFilesDone = false;
        static bool comparisonDone = false;
        static bool copyDone = false;
        static bool cacheDone = false;

        //counter
        static int copyCount = 0;
        static Stopwatch stopwatch;

        static char splitchar = '?';

        /// <summary>
        /// Differential backup from A to B
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
           
            if(args.Length!=2)
            {
                Console.WriteLine("You need 2 arguments with this command : source & destination");
                Console.ReadKey();
                return;
            }

            sourceFiles = new Dictionary<string, DateTime>();
            destinationFiles = new Dictionary<string, DateTime>();
            copyFiles = new Dictionary<string, DateTime>();        

            //arg[0] = Source
            //arg[1] = Destination
            source = args[0];
            destination = args[1];
            destinationCacheFile  = Convert.ToString(@"backupDav.txt");
            logFile = @"backupTool.log";


            //check here if paths are good
            if (!Directory.Exists(destination))
            {
                DialogResult dr;
                do
                {
                    dr = MessageBox.Show("Can't access destination folder " + destination , "Backup Tool", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Cancel) return;
                } while (!Directory.Exists(destination));

            }

            if (!Directory.Exists(source))
            {
                DialogResult dr;
                do
                {
                    dr = MessageBox.Show("Can't access source folder " + source, "Backup Tool", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Cancel) return;
                } while (!Directory.Exists(source));

            }

            Thread infoThread = new Thread(new ThreadStart(getInfo));
            Thread sourceFilesThread = new Thread(new ThreadStart(getSourceFiles));
            Thread destinationFilesThread = new Thread(new ThreadStart(getDestinationFiles));
            Thread comparingDictThread = new Thread(new ThreadStart(compareDictionnaries));
            Thread copyFilesThread = new Thread(new ThreadStart(copyFilesT));
            Thread createCacheThread = new Thread(new ThreadStart(createCache));

            infoThread.Start();
            sourceFilesThread.Start();
            destinationFilesThread.Start();

            addLogEvent(String.Format("Starting ...\t{0}\t->\t{1}", source, destination));
            
            //wait for processes to complete
            while (!sourceFilesDone || !destinationFilesDone){ }
            sourceFilesThread.Abort();
            destinationFilesThread.Abort();

            //Do comparison
            comparingDictThread.Start();
            while (!comparisonDone) { }
            comparingDictThread.Abort();

            copyFilesThread.Start();
            while (!copyDone) { }
            copyFilesThread.Abort();

            createCacheThread.Start();
            while (!cacheDone) { }
            createCacheThread.Abort();

            //to give time to display time
            Thread.Sleep(1000);
            comparingDictThread.Abort();
            
            
            infoThread.Abort();
            addLogEvent(String.Format("Done ! \tCopied :\t{0} files.", copyFiles.Count));

        }

        /// <summary>
        /// Thread to display info in the console
        /// </summary>
        static void getInfo()
        {
            getInfoHeader();

            stopwatch = new Stopwatch();
            stopwatch.Start();
          
            while (!sourceFilesDone || !destinationFilesDone) 
            {
                Console.SetCursorPosition(0, 5);
                Console.WriteLine("(1/4)\tAnalyzing files :\n");
                Console.WriteLine("\tSource Files Analyzed\t\t:\t{0}", sourceFiles.Count);
                Console.WriteLine("\tDestination Files Analyzed\t:\t{0}", destinationFiles.Count);
                Thread.Sleep(100);               
            }
            getInfoHeader();
            while (!comparisonDone) 
            {
                Console.SetCursorPosition(0, 5);
                Console.WriteLine("(2/4)\tComparing Files :\n");
                Console.WriteLine("\tFiles to copy\t:\t{0}", copyFiles.Count);
                Thread.Sleep(100);  
            }
            getInfoHeader();
            while (!copyDone) 
            {
                Console.SetCursorPosition(0, 5);
                Console.WriteLine("(3/4)\tCopying Files :\n");
                Console.WriteLine("\t{0}/{1}\t->\t{2}", copyCount, copyFiles.Count,copyFiles.ElementAt(copyCount-1).Key.PadRight(Console.WindowWidth*5,' '));
                Thread.Sleep(10); 
            }
            getInfoHeader();
            while (!cacheDone)
            {
                Console.SetCursorPosition(0, 5);
                Console.WriteLine("(4/4)\tCreating Cache :\n");
                Console.WriteLine("\t{0}/{1}", copyCount, sourceFiles.Count);
                Thread.Sleep(10);
            }
            stopwatch.Stop();
            getInfoHeader();
            Console.WriteLine("\tBackup Completed :\n");
            Console.WriteLine("\tTime\t:\t{0}", stopwatch.Elapsed);
            Thread.Sleep(10);
            

        }

        static void getInfoHeader()
        {
            Console.Clear();
            Console.WriteLine("Backing Tool v0.1 - David Davenne\n");
            Console.WriteLine("FROM:\t{0}", source);
            Console.WriteLine("TO:\t{0}", destination);
            Console.WriteLine();
        }

        static void compareDictionnaries()
        {
            IEnumerable<KeyValuePair<string,DateTime>> plop=sourceFiles.Except(destinationFiles, new myDictionnaryComparer());
            foreach(KeyValuePair<string,DateTime> x in plop)
            {
                copyFiles.Add(x.Key,x.Value);
            }
            Thread.Sleep(2000);
            comparisonDone = true;
        }

        /// <summary>
        /// Copy file from source to destination
        /// </summary>
        static void copyFilesT()
        {
            foreach(KeyValuePair<string,DateTime> x in copyFiles)
            {
                copyCount++;
                string destinationPath = destination+x.Key.Replace(source, "");
                try
                {                 
                    System.IO.FileInfo file = new System.IO.FileInfo(destinationPath);
                    file.Directory.Create();
                    System.IO.File.Copy(x.Key, destinationPath, true);
                }
                catch ( Exception)
                {
                     //skip file
                }
                
            }
            copyDone = true;
        }

        /// <summary>
        /// Create cache file containing the list of files that have been backed up
        /// </summary>
        static void createCache()
        {
            //create cacheFile here
            copyCount = 0;
            System.IO.StreamWriter file = new System.IO.StreamWriter(@destinationCacheFile);

            foreach(KeyValuePair<string,DateTime> x in sourceFiles)
            {
                copyCount++;
                file.WriteLine("{0}{2}{1}", x.Key, x.Value.ToUniversalTime(),splitchar);
                
            }
            file.Close();

            //copy cache file to destination directory
            System.IO.File.Copy(@destinationCacheFile, destination+"\\"+destinationCacheFile, true);
            //delete cache file @ source
            System.IO.File.Delete(@destinationCacheFile);          
            
            cacheDone = true;
        }

        /// <summary>
        /// Get source files and put them in the dictionary
        /// </summary>
        static void getSourceFiles()
        {

            //copy files only when files have been copied to the destination directory
          
        
           foreach (string f in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
           {
              try
              { 
                  DateTime dt = File.GetLastWriteTimeUtc(f);
                  sourceFiles.Add(f, dt);
                  
              }
              catch (PathTooLongException ex)
              {
                         //   Do something here
              }
              Thread.Sleep(1);
            }

           //
            sourceFilesDone = true;
        }

        /// <summary>
        /// Get Destination files and put them in the dictionary
        /// </summary>
        static void getDestinationFiles()
        {
            //check if cachefile exists at destination
            if (File.Exists(destination + "\\" + destinationCacheFile))
            {
                //if yes, copy cachefile
                System.IO.File.Copy(destination + "\\" + destinationCacheFile,@destinationCacheFile , true);
                //get files from cacheFile
                System.IO.StreamReader file = new System.IO.StreamReader(@destinationCacheFile);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    string[] split = line.Split(splitchar);
                    destinationFiles.Add(Convert.ToString(split[0]), Convert.ToDateTime(split[1]));
                }
                file.Close();
            }
            else
            {
                //if no cache file, we are gonna need to check all the files manually
                foreach (string f in Directory.GetFiles(
               destination,
               "*",
               SearchOption.AllDirectories))
                {
                    try
                    {
                        destinationFiles.Add(f, File.GetLastWriteTimeUtc(f));
                    }
                    catch (Exception)
                    {
                        //skip file
                    }
                    Thread.Sleep(1);
                }
            }
            destinationFilesDone = true;
        }

        static void addLogEvent(string logEvent)
        {
            StreamWriter file=System.IO.File.AppendText(logFile);
            file.WriteLine("{0}\t{1}",DateTime.UtcNow,logEvent);
            file.Close();
        }

    }

    class myDictionnaryComparer : IEqualityComparer<KeyValuePair<string,DateTime>>
    {
        public bool Equals(KeyValuePair<string,DateTime> x, KeyValuePair<string,DateTime> y)
        {
            return (x.Key == y.Key && x.Value.Date == y.Value.Date && x.Value.Hour == y.Value.Hour && x.Value.Minute == y.Value.Minute && x.Value.Second == y.Value.Second);
        }
        public int GetHashCode(KeyValuePair<string,DateTime> x)
        {
            return x.Key.GetHashCode();
        }
    }
}
