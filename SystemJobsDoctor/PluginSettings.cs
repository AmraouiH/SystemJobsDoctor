using System;
using System.Collections.Generic;

namespace SystemJobsDoctor
{
    /// <summary>
    /// Préférences du plugin, persistées par le SettingsManager de XrmToolBox (sérialisation XML).
    /// </summary>
    public class PluginSettings
    {
        /// <summary>Langue de l'interface : « en » (défaut) ou « fr ».</summary>
        public string Language { get; set; } = "en";

        /// <summary>Nombre maximal de jobs chargés pour l'analyse détaillée (les compteurs serveur ne sont pas limités).</summary>
        public int MaxRecords { get; set; } = 20000;

        /// <summary>Taille des lots ExecuteMultiple pour les actions en masse.</summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>Requêtes d'agrégat envoyées en parallèle pour les comptages (1 à 8).</summary>
        public int ParallelQueries { get; set; } = 4;

        /// <summary>Taille moyenne estimée d'un job (Ko), utilisée pour estimer la place gagnée.</summary>
        public int AverageJobSizeKb { get; set; } = 10;

        /// <summary>Dernière période choisie (index de la liste).</summary>
        public int PeriodIndex { get; set; } = 2;

        /// <summary>Dates de la période personnalisée (heure locale) ; null = borne décochée.</summary>
        public DateTime? CustomFrom { get; set; }

        public DateTime? CustomTo { get; set; }

        /// <summary>Colonne de date de la période : 0 = création, 1 = démarrage, 2 = fin.</summary>
        public int DateFieldIndex { get; set; }

        /// <summary>Derniers statuts cochés (valeurs de statuscode).</summary>
        public List<int> Statuses { get; set; } = new List<int> { 0, 10, 31 };

        /// <summary>Dernier type de job choisi (index de la liste).</summary>
        public int TypeIndex { get; set; }

        /// <summary>Règle de nettoyage : ancienneté minimale (jours).</summary>
        public int CleanupOlderThanDays { get; set; } = 30;

        public bool CleanupSucceeded { get; set; } = true;

        public bool CleanupCanceled { get; set; } = true;

        public bool CleanupFailed { get; set; }
    }
}
