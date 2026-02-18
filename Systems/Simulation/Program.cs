Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
Console.WriteLine("║            DIGITAL TWIN SIMULATION - TEST RUN               ║");
Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

// Display available test cases
TestCases.ListAll();
Console.Write("\nSelect test case number (0 - " + (TestCases.All.Count - 1) + "): ");
var selectionInput = Console.ReadLine();

if (int.TryParse(selectionInput, out var testIndex))
{
    // Components are initialized with test data when test runs
    await TestCases.RunByIndexAsync(testIndex);
}
else
{
    Console.WriteLine("Invalid selection. No test case executed.");
}
