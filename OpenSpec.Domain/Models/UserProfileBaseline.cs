using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public class UserProfileBaseline
    {
        public string PrincipalName { get; set; } = string.Empty;
        public DateTime LearningWindowEnd { get; set; }
        public double AvgRowsPerQuery { get; set; }
        public double StdDevRowsPerQuery { get; set; }
        public HashSet<string> KnownHosts { get; set; } = new();
        public HashSet<string> FrequentTables { get; set; } = new();
        public List<int> CommonAccessHours { get; set; } = new();
        public TimeSpan NormalWorkingHoursStart { get; set; }
        public TimeSpan NormalWorkingHoursEnd { get; set; }

        public bool IsColdStart(DateTime currentTime) => currentTime < LearningWindowEnd;
    }
}
