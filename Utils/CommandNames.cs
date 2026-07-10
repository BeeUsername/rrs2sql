using RRStoSQL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRStoSQL.Utils;
{
    internal static class CommandNames
    {
        public static string READ_CONFIG = "Читаем конфиг";
    }

interface ICommand
{
    string Name { get; }
    bool Required { get; }

    void Execute(AppConfig config);
}

class ReadConfigCommand : ICommand
{
    public string Name => CommandNames.READ_CONFIG;
    public bool Required => true;
    public void Execute(AppConfig config)
    {
        // Implementation for reading the config
        Console.WriteLine("Executing ReadConfigCommand...");
        // Add logic to read the configuration and populate the AppConfig object
    }
}

class CommandManager
{
    public List<ICommand> Commands = new List<ICommand>()
    {
        new ReadConfigCommand(),
    };
}
