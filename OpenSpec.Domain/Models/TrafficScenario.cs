using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSpec.Domain.Models
{
    public enum TrafficScenario
    {
        NormalAnalystActivity,    // Consultas pequeñas dentro de baseline
        DataExfiltrationAttempt,  // Select masivo (RowsPerQuery alto)
        UnusualHostAccess,        // Acceso desde una IP/Host no conocido
        SqlInjectionProbe,        // Inyección de SQL o acciones administrativas no habituales
        FullSimulation            // Mezcla de comportamiento normal con picos anómalos
    }
}
