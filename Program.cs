using System.Collections;
using System.Diagnostics;

namespace StockCutting;

class Program
{
    private static int _plankLength;
    private static int MaxPlanks = int.MaxValue;
    private static int MaxRemainder = int.MaxValue;
    private static List<Node> BestSlices = [];

    private static List<long> Times = [];
    private static List<int> Reminders = [];

    private class Node: ICloneable
    {
        public List<Plank> Slicing = [];
        public List<int> RemainingDetails = [];

        public int TotalRemainder => Slicing.Sum(s => s.Reminder);
        public int BestPossibleRemainder
        {
            get
            {
                var rem = TotalRemainder - RemainingDetails.Sum();
                if (rem < 0) rem += ((-1 * rem) / _plankLength + ((-1 * rem) % _plankLength > 0 ? 1 : 0)) * _plankLength;
                return rem;
            }
        }

        public int BestPossiblePlanks => (RemainingDetails.Sum() - TotalRemainder) / _plankLength + TotalPlanks;
        public int TotalPlanks => Slicing.Count;
        
        public void SortDetails()
        {
            RemainingDetails.Sort();
        }
        
        public void SortPlanks()
        {
            Slicing.Sort((x, y) => y.Reminder.CompareTo(x.Reminder));
        }

        public object Clone() =>
            new Node
            {
                RemainingDetails = [..RemainingDetails],
                Slicing = Slicing.ConvertAll(plank => (Plank)plank.Clone())
            };
    }

    private struct Plank(int detailLength, List<int>? stack = null): ICloneable
    {
        public int Reminder { get; set; } = _plankLength - detailLength;
        public List<int> Stack { get; set; } = stack ?? [];
        
        public object Clone() => new Plank(_plankLength - Reminder, new List<int>(Stack));
    }

    static double ComputeK(List<int> details, int tMax)
    {
        int len = details.Count;
        int sum = 0, sumSquare = 0;
        for (int i = 0; i < len; i++)
        {
            sum += details[i];
            sumSquare += details[i] * details[i];
        }

        float M = sum / len;
        float D = (sumSquare / len) + M * M;
        double sigma = Math.Sqrt(D);
        double k = tMax - 3 * sigma;
        return k;
    }
    
    private static void Main()
    {
        List<int> startDetails;
        
       
        var fileName = "data.txt";
        string filePath = Path.Combine(@"", fileName);
        Console.WriteLine("0-эвристика, 1 - точные");
        var mode = int.Parse(Console.ReadLine());
        if (File.Exists(filePath))
        {
            using StreamReader sr = new StreamReader(filePath);
            int number_experiment = 1;
            while (sr.ReadLine() is { } line)
            {
                MaxPlanks = int.MaxValue;
                MaxRemainder = int.MaxValue;
                BestSlices = [];
                _plankLength = int.Parse(line);
                if ((line = sr.ReadLine()) == null)
                    break;
                
                startDetails = line.Split(' ').Select(int.Parse).OrderByDescending(n => n).ToList();
                Console.WriteLine("Эксперимент №" + number_experiment++);
                var k = _plankLength * 0.2; //ComputeK(startDetails, startDetails.Max());
                var largeDetails = new List<int>();
                var smallDetails = new List<int>();
                foreach (var detail in startDetails)
                {
                    if (detail <= k)
                        smallDetails.Add(detail);
                    else
                        largeDetails.Add(detail);
                }
                Stopwatch stopwatch = Stopwatch.StartNew();
                if (mode == 0)
                {
                    BranchAndBounds(largeDetails);
                    if ((float)BestSlices[0].TotalRemainder / (BestSlices[0].TotalPlanks * _plankLength) < 0.15 ||
                        (float)largeDetails.Count / startDetails.Count < 0.15 ||
                        (float)largeDetails.Count / startDetails.Count > 0.9)
                    {
                        MaxPlanks = int.MaxValue;
                        MaxRemainder = int.MaxValue;
                        BestSlices[0].RemainingDetails = smallDetails;
                        BranchAndBounds(smallDetails, (Node)BestSlices[0].Clone());
                    }
                    else
                    {
                        Greedy(smallDetails, BestSlices[0]);
                    }
                    
                }
                else
                {
                    BranchAndBounds(startDetails);
                }

                stopwatch.Stop();
                Times.Add(stopwatch.ElapsedMilliseconds);
                Reminders.Add(BestSlices[0].TotalRemainder);
            }
            
            WriteResultsToFile(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_results.txt"));
        }
    }

    private static void WriteResultsToFile(string filePath)
    {
        using StreamWriter sw = new StreamWriter(filePath, false);
        sw.WriteLine("Времена");
        sw.Write("[");
        sw.Write(string.Join(", ", Times));
        sw.Write("] \n Остатки \n [");
        sw.Write(string.Join(", ", Reminders));
        sw.Write("]");
        sw.Close();
    }
    
    private static Node Greedy(List<int> details, Node slicing)
    {
        details.Sort();
        
        foreach (var detail in details)
        {
            slicing.SortPlanks();
            var totalPlanks = slicing.TotalPlanks;
            int i;
            for (i = 0; i < totalPlanks; i++)
            {
                if (slicing.Slicing[i].Reminder >= detail)
                {
                    slicing.Slicing[i] = new Plank(_plankLength - slicing.Slicing[i].Reminder + detail,
                        new List<int>(slicing.Slicing[i].Stack) { detail });
                    break;
                }
            }
            
            if(i == totalPlanks)
                slicing.Slicing.Add(new Plank(detail, [detail]));
        }

        return slicing;
    }
    
    
    private static void BranchAndBounds(List<int> startDetails, Node? root = null)
    {
        if (root is null)
        {
            root = new Node
            {
                RemainingDetails = startDetails
            };
        }

        root.SortDetails();

        Stack<Node> stack = new();

        stack.Push(root);

        while (stack.Count > 0 && MaxRemainder != 0)
        {
            var currentNode = stack.Pop();
            if (currentNode.BestPossiblePlanks > MaxPlanks) continue;
            if (currentNode.RemainingDetails.Count == 0)
            {
                var totalRemainder = currentNode.TotalRemainder;
                if (totalRemainder >= MaxRemainder) continue;
                MaxRemainder = totalRemainder;
                MaxPlanks = currentNode.TotalPlanks;
                BestSlices = [currentNode];
            }
            else
            {
                currentNode.SortPlanks();
                var detail = currentNode.RemainingDetails[^1];

                if (currentNode.TotalPlanks <= MaxPlanks - 1)
                {
                    var nodeWithNewPlank = (Node)currentNode.Clone();
                    nodeWithNewPlank.RemainingDetails.RemoveAt(currentNode.RemainingDetails.Count - 1);
                    nodeWithNewPlank.Slicing.Add(new Plank(detail, [detail]));
                    if(nodeWithNewPlank.BestPossibleRemainder < MaxRemainder)
                        stack.Push(nodeWithNewPlank);
                }
                var totalPlanks = currentNode.TotalPlanks;
                var prevReminder = -1;
                for (var plankIndex = 0; plankIndex < totalPlanks; plankIndex++)
                {
                    var plank = currentNode.Slicing[plankIndex];
                    if (plank.Reminder < detail) break;
                    
                    if(plank.Reminder == prevReminder) continue;
                    prevReminder = plank.Reminder;

                    var newNode = (Node)currentNode.Clone();
                    newNode.RemainingDetails.RemoveAt(currentNode.RemainingDetails.Count - 1);
                    var newStack = new List<int>(plank.Stack) { detail };
                    var updatedPlank = new Plank
                    {
                        Reminder = plank.Reminder - detail,
                        Stack = newStack
                    };
                    newNode.Slicing[plankIndex] = updatedPlank;
                    if(newNode.BestPossibleRemainder < MaxRemainder)
                        stack.Push(newNode);
                }
            }
        }
    }
}