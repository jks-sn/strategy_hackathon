using System;
using System.Collections.Generic;
using System.Linq;
using Nsu.HackathonProblem.Contracts;

namespace Nsu.HackathonProblem;

// Мне не нравится гармоничность в кач-ве фитнес-функции и функции в венгерском По-моему она как промежуточная
// метрика сликшом нестоучива, особенно к большим выбросам, так что можно ещё попрбовать другую функцию(
// например среднее арифметичкое или ещё что - то прикольное)

public class HungarianGAOptimizedStrategy : ITeamBuildingStrategy
{
    public int PopulationSize = 30; // размер популяции
    public int Generations = 50; // число поколений 
    public double MutationRate = 0.5; // вероятность мутации потомка
    private readonly Random _random = new Random();

    public IEnumerable<Team> BuildTeams(
        IEnumerable<Employee> teamLeads,
        IEnumerable<Employee> juniors,
        IEnumerable<Wishlist> teamLeadsWishlists,
        IEnumerable<Wishlist> juniorsWishlists)
    {
        var teamLeadsList = teamLeads.ToList();
        var juniorsList = juniors.ToList();

        var teamLeadPrefDict = teamLeadsWishlists.ToDictionary(w => w.EmployeeId, w => w.DesiredEmployees);
        var juniorPrefDict = juniorsWishlists.ToDictionary(w => w.EmployeeId, w => w.DesiredEmployees);

        // 1) Генерируем базовое решение венгерским алгоритмом по "сумме" Satisfaction
        // Построим costMatrix: cost[i,j] = 40 - ( (20 - tlRank) + (20 - jrRank) )
        int n = teamLeadsList.Count; // по условию 20
        var costMatrix = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            var tl = teamLeadsList[i];
            var tlWishlist = teamLeadPrefDict[tl.Id];
            for (int j = 0; j < n; j++)
            {
                var jr = juniorsList[j];
                var jrWishlist = juniorPrefDict[jr.Id];

                int tlRank = Array.IndexOf(tlWishlist, jr.Id);
                int jrRank = Array.IndexOf(jrWishlist, tl.Id);

                int sumSatisfaction = (20 - tlRank) + (20 - jrRank);
                costMatrix[i, j] = 40 - sumSatisfaction;
            }
        }

        int[] hungarianMatch = HungarianAlgorithm.Solve(costMatrix);

        // 2) Запускаем генетический алгоритм, оптимизирующий "гармоническое среднее".
        //    Модель: одна хромосома = permutation size n, где chrom[i] = j, то есть TL i -> JR j.
        //    Фитнесс = harmonicMean(chrom).
        var bestSolution = (int[])hungarianMatch.Clone();
        double bestFitness =
            ComputeHarmonicMean(bestSolution, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict);

        // Сформируем начальную популяцию
        var population = new List<int[]>(PopulationSize);
        // несколько копий "венгерского" решения — "элита"
        population.Add(bestSolution);
        population.Add((int[])bestSolution.Clone());

        // добавим несколько случайных(для добивания до размера популяции)
        for (int i = population.Count; i < PopulationSize; i++)
        {
            population.Add(GenerateRandomMatching(n));
        }

        // Оценим фитнес всей популяции(фитнес-функция - пока гармоничность)
        var fitnessList = population
            .Select(chrom => ComputeHarmonicMean(chrom, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict))
            .ToList();

        // Запускаем цикл по поколениям
        for (int gen = 0; gen < Generations; gen++)
        {
            var newPopulation = new List<int[]>(PopulationSize);
            var newFitnessList = new List<double>(PopulationSize);

            // отборная элита (выбираем топ-2 из старой популяции)
            var eliteIndices = fitnessList
                .Select((val, idx) => (val, idx))
                .OrderByDescending(x => x.val)
                .Take(2)
                .ToList();
            foreach (var elite in eliteIndices)
            {
                newPopulation.Add(population[elite.idx]);
                newFitnessList.Add(elite.val);
            }

            // Создаём потомков, пока не наберем PopulationSize
            while (newPopulation.Count < PopulationSize)
            {
                // Выбираем родителей турнирной селекцией
                var p1 = TournamentSelect(population, fitnessList, 3);
                var p2 = TournamentSelect(population, fitnessList, 3);

                // Кроссовер
                var child = Crossover(p1, p2);

                // Мутация
                if (_random.NextDouble() < MutationRate)
                {
                    MutationSwap(child);
                }

                // Локальный поиск — пара свайпов(а точно ли тут стоит?)
                LocalSearchPairwise(child, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict);

                // Считаем фитнесс (гармоничность)
                double childFitness =
                    ComputeHarmonicMean(child, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict);

                // добавляем в новую популяцию
                newPopulation.Add(child);
                newFitnessList.Add(childFitness);

                // проверяем глобальный best
                if (childFitness > bestFitness)
                {
                    bestFitness = childFitness;
                    bestSolution = (int[])child.Clone();
                }
            }

            // новая популяция -> текущая
            population = newPopulation;
            fitnessList = newFitnessList;
        }

        // В bestSolution лежит хромосома c максимальной гармоничностью
        // Формируем Team[]
        var finalTeams = new List<Team>(n);
        for (int i = 0; i < n; i++)
        {
            finalTeams.Add(new Team(teamLeadsList[i], juniorsList[bestSolution[i]]));
        }

        return finalTeams;
    }
    
    private int[] GenerateRandomMatching(int n)
    {
        var arr = Enumerable.Range(0, n).ToArray();
        for (int i = 0; i < n; i++)
        {
            int r = _random.Next(i, n);
            (arr[i], arr[r]) = (arr[r], arr[i]);
        }

        return arr;
    }
    
    private double ComputeHarmonicMean(
        int[] match,
        List<Employee> teamLeadsList,
        List<Employee> juniorsList,
        Dictionary<int, int[]> teamLeadPrefDict,
        Dictionary<int, int[]> juniorPrefDict)
    {
        var satisfactionValues = new List<int>(match.Length * 2);
        for (int i = 0; i < match.Length; i++)
        {
            int j = match[i];
            var tl = teamLeadsList[i];
            var jr = juniorsList[j];

            int[] tlWish = teamLeadPrefDict[tl.Id];
            int tlPos = Array.IndexOf(tlWish, jr.Id);
            int tlSat = 20 - tlPos;

            int[] jrWish = juniorPrefDict[jr.Id];
            int jrPos = Array.IndexOf(jrWish, tl.Id);
            int jrSat = 20 - jrPos;

            satisfactionValues.Add(tlSat);
            satisfactionValues.Add(jrSat);
        }

        double sumRecip = 0.0;
        foreach (var s in satisfactionValues)
            sumRecip += 1.0 / s;

        double n = satisfactionValues.Count; // 40
        return n / sumRecip;
    }
    
    private int[] TournamentSelect(List<int[]> population, List<double> fitnessList, int k)
    {
        int bestIdx = -1;
        double bestFit = double.MinValue;
        for (int i = 0; i < k; i++)
        {
            int idx = _random.Next(population.Count);
            if (fitnessList[idx] > bestFit)
            {
                bestFit = fitnessList[idx];
                bestIdx = idx;
            }
        }

        return (int[])population[bestIdx].Clone();
    }
    
    private int[] Crossover(int[] p1, int[] p2)
    {
        int n = p1.Length;
        var child = new int[n];
        Array.Fill(child, -1);

        // возьмём первую половину из p1
        var chosen = new HashSet<int>();
        for (int i = 0; i < n / 2; i++)
        {
            child[i] = p1[i];
            chosen.Add(p1[i]);
        }

        // вторую половину заполняем из p2, если конфликт - возьмём что-то не занятое
        for (int i = n / 2; i < n; i++)
        {
            int candidate = p2[i];
            if (!chosen.Contains(candidate))
            {
                child[i] = candidate;
                chosen.Add(candidate);
            }
        }

        // Проходимся по child, где -1, вставляем «свободных» джунов
        int[] freeJuniors = Enumerable.Range(0, n).Where(x => !chosen.Contains(x)).ToArray();
        int freePtr = 0;
        for (int i = 0; i < n; i++)
        {
            if (child[i] == -1)
            {
                child[i] = freeJuniors[freePtr++];
            }
        }

        return child;
    }
    
    private void MutationSwap(int[] chrom)
    {
        int n = chrom.Length;
        int a = _random.Next(n);
        int b = _random.Next(n);
        (chrom[a], chrom[b]) = (chrom[b], chrom[a]);
    }
    
    private void LocalSearchPairwise(
        int[] match,
        List<Employee> teamLeadsList,
        List<Employee> juniorsList,
        Dictionary<int, int[]> teamLeadPrefDict,
        Dictionary<int, int[]> juniorPrefDict)
    {
        int n = match.Length;
        double currentHarm = ComputeHarmonicMean(match, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict);
        for (int attempt = 0; attempt < 5; attempt++) // пару итераций — довольно
        {
            bool improved = false;
            for (int i = 0; i < n; i++)
            {
                for (int k = i + 1; k < n; k++)
                {
                    // пытаемся свапнуть джунов
                    int ji = match[i];
                    int jk = match[k];

                    match[i] = jk;
                    match[k] = ji;

                    double newHarm = ComputeHarmonicMean(match, teamLeadsList, juniorsList, teamLeadPrefDict,
                        juniorPrefDict);
                    if (newHarm > currentHarm)
                    {
                        currentHarm = newHarm;
                        improved = true;
                    }
                    else
                    {
                        // откат
                        match[i] = ji;
                        match[k] = jk;
                    }
                }
            }

            if (!improved) break;
        }
    }
}

public static class HungarianAlgorithm
{
    public static int[] Solve(int[,] costMatrix)
    {
        int n = costMatrix.GetLength(0);
        int[] u = new int[n];
        int[] v = new int[n];
        int[] p = new int[n];
        int[] way = new int[n];

        for (int i = 1; i < n; i++)
        {
            p[0] = i;
            int j0 = 0;
            int[] minv = new int[n];
            bool[] used = new bool[n];
            for (int j = 1; j < n; j++)
            {
                minv[j] = int.MaxValue;
            }

            int j1 = 0;
            do
            {
                used[j0] = true;
                int i0 = p[j0];
                int delta = int.MaxValue;
                j1 = 0;
                for (int j = 1; j < n; j++)
                {
                    if (!used[j])
                    {
                        int cur = costMatrix[i0, j] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }

                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                }

                for (int j = 0; j < n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        int[] matchRow = new int[n];
        for (int j = 0; j < n; j++)
        {
            matchRow[p[j]] = j;
        }

        return matchRow;
    }
}