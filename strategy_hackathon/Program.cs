using System.Diagnostics;
using Nsu.HackathonProblem;
using Nsu.HackathonProblem.Contracts;

namespace TeamBuildingStrategy;

class Program
{
    public TimeSpan ExecutionTime { get; set; }
    static void Main(string[] args)
    {
        var random = new Random();
        var stopwatch = new Stopwatch();
        const int hackathonRuns = 1000;

        var juniors = LoadEmployeesFromCsv("Juniors20.csv");
        var teamLeads = LoadEmployeesFromCsv("Teamleads20.csv");

        var strategy = new HungarianGAOptimizedStrategy();
        var harmonicMeans = new List<double>();
        stopwatch.Restart();
        for (int run = 0; run < hackathonRuns; run++)
        {
                var teamLeadsWishlists = teamLeads.Select(tl =>
                    new Wishlist(tl.Id, GenerateRandomWishlist(juniors.Select(j => j.Id).ToArray(), random))).ToList();

                var juniorsWishlists = juniors.Select(j =>
                    new Wishlist(j.Id, GenerateRandomWishlist(teamLeads.Select(tl => tl.Id).ToArray(), random))).ToList();

            var teams = strategy.BuildTeams(teamLeads, juniors, teamLeadsWishlists, juniorsWishlists);

            double harmonicMean = CalculateHarmonicMean(teams, teamLeadsWishlists, juniorsWishlists);
            Console.WriteLine($"Harmonic Mean for Run {run + 1}: {harmonicMean:F2}");
            harmonicMeans.Add(harmonicMean);
        }
        stopwatch.Stop();
        var executionTime = stopwatch.Elapsed;
        Console.WriteLine($"Average Harmonic Satisfaction: {harmonicMeans.Average():F2}\n");
        Console.WriteLine($"Execution Time: {executionTime:hh\\:mm\\:ss}\n");
    }

    private static int[] GenerateRandomWishlist(int[] options, Random random)
    {
        return options.OrderBy(_ => random.Next()).ToArray();
    }


    private static double CalculateHarmonicMean(
        IEnumerable<Team> teams,
        IEnumerable<Wishlist> teamLeadsWishlists,
        IEnumerable<Wishlist> juniorsWishlists)
    {
        var satisfactionIndices = CalculateSatisfactionIndices(teams, teamLeadsWishlists, juniorsWishlists);

        int n = satisfactionIndices.Count;
        double sumOfReciprocals = 0;

        foreach (var index in satisfactionIndices)
        {
            if (index > 0)
            {
                sumOfReciprocals += 1.0 / index;
            }
        }

        return n / sumOfReciprocals;
    }

    private static List<int> CalculateSatisfactionIndices(
        IEnumerable<Team> teams,
        IEnumerable<Wishlist> teamLeadsWishlists,
        IEnumerable<Wishlist> juniorsWishlists)
    {
        var satisfactionIndices = new List<int>();
        foreach (var team in teams)
        {
            var teamLeadWishlist = teamLeadsWishlists.First(w => w.EmployeeId == team.TeamLead.Id).DesiredEmployees;
            var juniorWishlist = juniorsWishlists.First(w => w.EmployeeId == team.Junior.Id).DesiredEmployees;

            int juniorSatisfaction = GetSatisfactionScore(juniorWishlist, team.TeamLead.Id);
            int teamLeadSatisfaction = GetSatisfactionScore(teamLeadWishlist, team.Junior.Id);

            satisfactionIndices.Add(juniorSatisfaction);
            satisfactionIndices.Add(teamLeadSatisfaction);
        }

        return satisfactionIndices;
    }
    private static int GetSatisfactionScore(int[] wishlist, int assignedPartner)
    {
        int position = Array.IndexOf(wishlist, assignedPartner);
        return 20 - position;
    }
    private static IEnumerable<Employee> LoadEmployeesFromCsv(string filePath)
    {
        return File.ReadLines(filePath)
            .Skip(1)
            .Select(line => line.Split(';'))
            .Select(parts => new Employee(int.Parse(parts[0]), parts[1]))
            .ToList();
    }
}