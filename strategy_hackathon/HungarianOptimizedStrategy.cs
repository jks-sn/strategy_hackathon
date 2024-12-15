using System;
using System.Collections.Generic;
using System.Linq;
using Nsu.HackathonProblem.Contracts;

namespace Nsu.HackathonProblem;
public class HungarianOptimizedStrategy : ITeamBuildingStrategy
{
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

        int n = teamLeadsList.Count;
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
        
        int[] matchJrForTl = HungarianAlgorithm.Solve(costMatrix);
        
        double bestHarm = ComputeHarmonicMean(matchJrForTl, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict);
        bool improved = true;

        while (improved)
        {
            improved = false;
            
            for (int i = 0; i < n; i++)
            {
                for (int k = i+1; k < n; k++)
                {
                    int j_i = matchJrForTl[i];
                    int j_k = matchJrForTl[k];
                    
                    matchJrForTl[i] = j_k;
                    matchJrForTl[k] = j_i;

                    double newHarm = ComputeHarmonicMean(matchJrForTl, teamLeadsList, juniorsList, teamLeadPrefDict, juniorPrefDict);

                    if (newHarm > bestHarm)
                    {
                        bestHarm = newHarm;
                        improved = true;
                    }
                    else
                    {
                        matchJrForTl[i] = j_i;
                        matchJrForTl[k] = j_k;
                    }
                }
            }
        }
        
        var result = new List<Team>(n);
        for (int i = 0; i < n; i++)
        {
            int j = matchJrForTl[i];
            var tl = teamLeadsList[i];
            var jr = juniorsList[j];
            result.Add(new Team(tl, jr));
        }

        return result;
    }
    private double ComputeHarmonicMean(
        int[] match,
        List<Employee> teamLeadsList,
        List<Employee> juniorsList,
        Dictionary<int, int[]> teamLeadPrefDict,
        Dictionary<int, int[]> juniorPrefDict)
    {
        var indices = new List<int>(match.Length * 2);

        for (int i = 0; i < match.Length; i++)
        {
            int j = match[i];
            var tl = teamLeadsList[i];
            var jr = juniorsList[j];
            
            int[] tlWishlist = teamLeadPrefDict[tl.Id];
            int tlPos = Array.IndexOf(tlWishlist, jr.Id);
            int tlSatisfaction = 20 - tlPos;
            
            int[] jrWishlist = juniorPrefDict[jr.Id];
            int jrPos = Array.IndexOf(jrWishlist, tl.Id);
            int jrSatisfaction = 20 - jrPos;

            indices.Add(tlSatisfaction);
            indices.Add(jrSatisfaction);
        }
        
        double sumRecip = 0;
        foreach (var si in indices)
        {
            sumRecip += 1.0 / si; 
        }
        double n = indices.Count; 
        double harmonic = n / sumRecip;
        return harmonic;
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
