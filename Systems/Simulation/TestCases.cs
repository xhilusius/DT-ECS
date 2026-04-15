using Simulation.ServiceManager.CompositeServices;

public static class TestCases
{

    public static void ListAll(IReadOnlyList<TestExecutorService.TestCaseSummary> cases)
    {
        Console.WriteLine("Available test cases:");
        for (int i = 0; i < cases.Count; i++)
            Console.WriteLine($"  [{i + 1}] {cases[i].Name}");
    }

}
