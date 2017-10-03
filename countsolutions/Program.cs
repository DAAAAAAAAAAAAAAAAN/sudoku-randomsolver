using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace countsolutions
{
    class Program
    {
        static Stopwatch timer = Stopwatch.StartNew();

        static string appTime()
        {
            return timer.Elapsed.TotalSeconds.ToString("0").PadLeft(5, ' ') + "s";
        }

        static string python(string cmd, string args)
        {
            return run_cmd(@"python.exe", cmd, args);
        }

        static string run_cmd(string file, string cmd, string args)
        {
            string output = "";
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = file;
            start.Arguments = string.Format("{0} {1}", cmd, args);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    output = reader.ReadToEnd();
                }
            }
            return output;
        }

        static List<string> loadFile(string file)
        {
            var lines = File.ReadAllLines(file).Select(a => a.Split(',')[0]);
            var csv = from line in lines
                      select (line.Split(',')[0]);
            return csv.ToList();
        }

        static List<Tuple<string, List<string>>> loadImproperSudokus (string file)
        {
            var contents = new List<Tuple<string, List<string>>>();
            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                var elements = line.Split(',');
                contents.Add(Tuple.Create(elements[0], elements[1]?.Split('|').ToList()));
            }
            return contents;
        }

        static void Main(string[] args)
        {
            appTime();
            var sourceFile = "";
            if (args.Length <= 0 || args[0].Length <= 0)
            {
                sourceFile = @"improper_sudokus.csv";
            }
            int startFromSudokuIndex = 0;
            try
            {
                startFromSudokuIndex = Int32.Parse(args[1]);
            }
            catch (Exception e) { }

            var csv = loadImproperSudokus(sourceFile);

            var outputPath = @"improper_sudokus_solution_counts.csv";

            int puzzleCounter = 0;
            foreach (var sudoku in csv)
            {
                while (startFromSudokuIndex-- > 0)
                {
                    continue;
                }
                puzzleCounter++;

                // convert sudoku to CNF with python
                File.WriteAllText(@"WIP_sudoku.txt", sudoku.Item1);
                python("sudoku-to-cnf.py", "-i WIP_sudoku.txt -o WIP_sudoku.cnf");

                /*// index solutions in CNF form
                var solutionMap = new Dictionary<string, int>();
                foreach (var solution in sudoku.Item2)
                {
                    // convert solution to CNF with python (retarded method)
                    File.WriteAllText(@"WIP_solution.txt", solution);
                    python("sudoku-to-cnf.py", "-i WIP_solution.txt -o WIP_solution.cnf");
                    var solverOutput = run_cmd(@"zchaff.exe", "", "WIP_solution.cnf");
                    var hasFoundSolution = !solverOutput.Contains(@"UNSAT");
                    if (hasFoundSolution)
                    {
                        // edit solution file to remove garbage from zchaff
                        var lines = solverOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        var solutionLine = lines[4];
                        var solutionCnfString = solutionLine.Substring(0, solutionLine.IndexOf("Random"));
                        solutionMap[solutionCnfString] = 0;
                    }
                    else
                    {
                        Console.WriteLine("uh oh");
                    }
                }*/

                var solutionsCount = sudoku.Item2.Count;
                var requiredRunCount = solutionsCount * 500;

                // solve sudoku many times
                var solverOutput2 = run_cmd(@"ubcsat.exe", "", "-alg saps -i WIP_sudoku.cnf -r solution -r out null -r stats null -cutoff 10000000 -runs " + requiredRunCount);

                // collect results
                var solutionMap2 = new Dictionary<string, int>();
                var runs = solverOutput2.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var run in runs)
                {
                    var blaat = run.Split(' ');
                    if (blaat.Length > 2)
                    {
                        continue;
                    }
                    var solution2 = blaat[1];
                    if (solutionMap2.ContainsKey(solution2))
                    {
                        solutionMap2[solution2]++;
                    }
                    else
                    {
                        solutionMap2[solution2] = 1;
                    }
                }

                // write results
                var csvLine = String.Format("{0},{1}", sudoku.Item1, String.Join(" ", solutionMap2.Select(a => a.Value)));
                if (solutionMap2.Count != sudoku.Item2.Count)
                {
                    // correct for solutions found 0 times
                    int missedSolutionsCount = sudoku.Item2.Count - solutionMap2.Count;
                    for (int i = 0; i < missedSolutionsCount; i++)
                    {
                        csvLine += " 0";
                    }
                }
                var csvLines = new List<string>();
                csvLines.Add(csvLine);
                File.AppendAllLines(outputPath, csvLines);
                
                Console.WriteLine("{0}: sudoku #{1} ran {2} times generating {3} counts.", appTime(), puzzleCounter, requiredRunCount, runs.Length-1);

                //if (puzzleCounter >= 250) break;
            }
            Console.ReadLine();
        }
    }
}
