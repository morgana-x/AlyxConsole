
public partial class Program
{
    public static void Main(string[] args)
    {
        VConsole vConsole = new VConsole();

        bool success = vConsole.Connect();
        if (!success)
        {
            throw new Exception("couldn't connect to vconsole!");
        }

        vConsole.VConsoleOnPrint += printVConsoleOutput;

        string inp = "";
        while (inp != "disconnect")
        {
            inp = Console.ReadLine();
            vConsole.SendCommand(inp);
        }
        vConsole.Disconnect();

    }

    private static void printVConsoleOutput(object sender, VConsole.VConsolePrintEventArgs e)
    {
        Console.Write(e.Message);
    }
}