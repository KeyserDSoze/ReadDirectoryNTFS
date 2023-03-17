using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace ReadDirectoryNTFS
{
    public sealed class Reader
    {
        private readonly StringBuilder _stringBuilder = new();
        private readonly string _fileName;
        private int _counter;
        private long _totalFolders;
        private int _megaMax = 25 * 1024 * 1024;
        public Reader()
        {
            var directory = string.Join('\\', Assembly.GetExecutingAssembly().Location.Split('\\').Take(Assembly.GetExecutingAssembly().Location.Split('\\').Length - 1));
            if (!Directory.Exists($"{directory}\\export"))
                Directory.CreateDirectory($"{directory}\\export");
            var fullDirectory = $"{DateTime.UtcNow:yyyyMMddHHmmss}";
            Directory.CreateDirectory($"{directory}\\export\\{fullDirectory}");
            _fileName = $"{directory}\\export\\{fullDirectory}\\part{{0}}.csv";
        }
        public async Task ExecuteAsync()
        {
            DateTime start = DateTime.UtcNow;
            Console.WriteLine("Insert a path to verify.");
            var line = Console.ReadLine();
            Console.WriteLine("Insert the max MB for each CSV file. If you press enter the default value of 25 will be used.");
            var maxMB = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(maxMB))
                _megaMax = int.Parse(maxMB) * 1024 * 1024;
            CheckDirectory(new(line!));
            Flush(true);
            Console.WriteLine($"{_totalFolders} folders inspected in {DateTime.UtcNow.Subtract(start).TotalSeconds} seconds.");
        }
        public string AsString(string input)
            => input.Replace(",", ";");
        private static readonly object s_trafficLight = new();
        public void CheckDirectory(DirectoryInfo directoryInfo)
        {
            DirectorySecurity acl = directoryInfo.GetAccessControl(AccessControlSections.All);
            AuthorizationRuleCollection rules = acl.GetAccessRules(true, true, typeof(NTAccount));
            foreach (AuthorizationRule rule in rules)
            {
                if (!rule.IdentityReference.Value.Contains("NT AUTHORITY\\SYSTEM") &&
                    !rule.IdentityReference.Value.Contains("BUILTIN\\"))
                {
                    lock (s_trafficLight)
                    {
                        _stringBuilder.Append(directoryInfo.FullName);
                        _stringBuilder.Append($",{rule.IdentityReference.Value},{AsString(rule.InheritanceFlags.ToString())},{AsString(rule.PropagationFlags.ToString())}");
                        if (rule is FileSystemAccessRule fileSystemRule)
                            _stringBuilder.Append($",{AsString(fileSystemRule.AccessControlType.ToString())},{AsString(fileSystemRule.FileSystemRights.ToString())}");
                        _stringBuilder.AppendLine();
                    }
                }
            }
            lock (s_trafficLight)
                _totalFolders++;
            Flush(false);
            //foreach (var diB in directoryInfo.GetDirectories())
            //    CheckDirectory(diB);
            Parallel.ForEach(directoryInfo.GetDirectories(), CheckDirectory);
        }
        public void Flush(bool isFinal = false)
        {
            if (isFinal || _stringBuilder.Length > _megaMax)
            {
                lock (s_trafficLight)
                {
                    if (isFinal || _stringBuilder.Length > _megaMax)
                    {
                        _counter++;
                        var stream = File.CreateText(string.Format(_fileName, _counter));
                        stream.Write(_stringBuilder.ToString());
                        stream.Flush();
                        stream.Close();
                        _stringBuilder.Remove(0, _stringBuilder.Length);
                        _stringBuilder.Append("Folder,IdentityReference,InheritanceFlags,PropagationFlags,AccessControlType,FileSystemRights");
                        _stringBuilder.AppendLine();
                    }
                }
            }
        }
    }
}
