using System;
using System.Linq;
using System.Text;
using SystemJobsDoctor.Models;
using SystemJobsDoctor.Services;
using SystemJobsDoctor.UI;

namespace SystemJobsDoctor.Localization
{
    public enum Language
    {
        English,
        French
    }

    /// <summary>
    /// Every user-facing text of the tool, in English and French.
    /// Changing <see cref="Current"/> raises <see cref="LanguageChanged"/> so open screens can refresh.
    /// </summary>
    public static class Strings
    {
        private static Language _current = Language.English;

        public static event EventHandler LanguageChanged;

        public static Language Current
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool IsFrench => _current == Language.French;

        public static string ToCode(Language language) => language == Language.French ? "fr" : "en";

        public static Language FromCode(string code) =>
            string.Equals(code, "fr", StringComparison.OrdinalIgnoreCase) ? Language.French : Language.English;

        private static string T(string english, string french) => IsFrench ? french : english;

        #region Header / toolbar

        public const string AppTitle = "System Jobs Doctor";
        public static string AppSubtitle => T("Diagnose, fix and clean up Dataverse system jobs", "Diagnostiquez, réparez et nettoyez les System Jobs Dataverse");
        public static string Close => T("Close", "Fermer");
        public static string CloseTip => T("Close the tool", "Fermer l'outil");
        public static string Help => T("Help", "Aide");
        public static string HelpTip => T("How to use the tool", "Comment utiliser l'outil");
        public static string HelpSubtitle => T("Diagnose, fix and clean up the system jobs in a few steps", "Diagnostiquer, réparer et nettoyer les System Jobs en quelques étapes");
        public static string HelpText => T(
            "1. Set the scope: period (start / end dates, on the created, started or completed date), type, table, name and statuses.\n" +
            "2. Click Analyze. Nothing is loaded before. A long loading can be stopped (Stop button or Esc).\n\n" +
            "DASHBOARD\n• Exact server counts per status, jobs over time, breakdowns by status, type, table and process.\n• Click a tile or a bar to list the jobs behind it.\n\n" +
            "ERRORS\n• Errors are normalized (GUIDs, dates, numbers, names removed): one line = one cause.\n• Top 10 failing processes with their failure rate.\n• Resume, cancel or delete the jobs of a whole group.\n\n" +
            "JOBS\n• Filter the list by table, search, sort. Ctrl / Shift to select several jobs, Ctrl+A for all.\n• Resume (suspended and failed jobs), Cancel, Postpone, Delete (completed jobs).\n• Summarize this job, Plug-in trace logs, open the record, the system job or the process.\n\n" +
            "STORAGE CLEANUP\n• Row count and size estimate of the system jobs table, count by status.\n• Cleanup rule, estimate, then a bulk deletion job (once or recurring) created in Dataverse.\n\n" +
            "Summary: text summary of the failed jobs, ready to paste. Export Excel: report of the analysis.",
            "1. Définissez le périmètre : période (dates de début / fin, sur la date de création, de démarrage ou de fin), type, table, nom et statuts.\n" +
            "2. Cliquez sur Analyser. Rien n'est chargé avant. Un chargement long peut être arrêté (bouton Arrêter ou Échap).\n\n" +
            "TABLEAU DE BORD\n• Compteurs serveur exacts par statut, jobs dans le temps, répartitions par statut, type, table et processus.\n• Cliquez sur une tuile ou une barre pour lister les jobs concernés.\n\n" +
            "ERREURS\n• Les erreurs sont normalisées (GUID, dates, nombres, noms retirés) : une ligne = une cause.\n• Top 10 des processus qui échouent, avec leur taux d'échec.\n• Reprenez, annulez ou supprimez les jobs d'un groupe entier.\n\n" +
            "JOBS\n• Filtrez la liste par table, recherchez, triez. Ctrl / Maj pour sélectionner plusieurs jobs, Ctrl+A pour tout.\n• Reprendre (jobs suspendus et en échec), Annuler, Reporter, Supprimer (jobs terminés).\n• Résumer ce job, Plugin Trace Log, ouvrir l'enregistrement, le System Job ou le processus.\n\n" +
            "NETTOYAGE DU STOCKAGE\n• Nombre de lignes et taille estimée de la table des System Jobs, comptage par statut.\n• Règle de nettoyage, estimation, puis job de suppression en bloc (ponctuel ou récurrent) créé dans Dataverse.\n\n" +
            "Résumé : synthèse texte des jobs en échec, prête à coller. Export Excel : rapport de l'analyse.");
        public static string Analyze => T("Analyze", "Analyser");
        public static string AnalyzeTip => T("Load the jobs of the scope and refresh every page", "Charge les jobs du périmètre et actualise toutes les pages");
        public static string ExportExcel => T("Export Excel", "Export Excel");
        public static string SummaryShort => T("Summary", "Résumé");
        public static string FailedSummary => T("Failed jobs summary", "Résumé des échecs");
        public static string FailedSummaryTip => T("Text summary of the failed jobs: causes, processes, tables and next steps, ready to paste",
                                                   "Résumé texte des jobs en échec : causes, processus, tables et prochaines étapes, prêt à coller");
        public static string FailedSummaryListTip => T("Summary of the failed jobs of this list (filters applied)", "Résumé des jobs en échec de cette liste (filtres appliqués)");
        public static string ExportTip => T("Excel report: summary, top 10 failing processes, error groups and every loaded job",
                                            "Rapport Excel : synthèse, top 10 des processus en échec, groupes d'erreurs et tous les jobs chargés");
        public static string Options => "Options";
        public static string OptionsSubtitle => T("Analysis limit, bulk action batches and storage estimate", "Limite d'analyse, lots des actions en masse et estimation du stockage");
        public static string LanguageMenu => T("Language", "Langue");
        public const string EnglishName = "English";
        public const string FrenchName = "Français";
        public static string Donate => T("Donate", "Faire un don");
        public static string DonateTip => T("Support the tool (PayPal)", "Soutenir l'outil (PayPal)");
        public const string AuthorName = "Hamza AMRAOUI";
        public static string AuthorTip => T("Developed by Hamza AMRAOUI (LinkedIn)", "Développé par Hamza AMRAOUI (LinkedIn)");
        public static string NotConnected => T("Not connected", "Non connecté");
        public static string ConnectedTo(string name) => T($"Connected to {name}", $"Connecté à {name}");

        #endregion

        #region Scope

        public static string ScopeTitle => T("Scope", "Périmètre");
        public static string ScopeSubtitle => T("Which system jobs to analyze: period (created on), type, regarding table, process name and statuses",
                                                "Quels System Jobs analyser : période (date de création), type, table concernée, nom du processus et statuts");
        public static string PeriodLabel => T("Period", "Période");
        public static string FromLabel => T("Start date", "Date de début");
        public static string ToLabel => T("End date", "Date de fin");
        public static string DateFieldLabel => T("on", "sur");
        public static string[] DateFields => IsFrench
            ? new[] { "Date de création", "Date de démarrage", "Date de fin" }
            : new[] { "Created on", "Started on", "Completed on" };
        public static string DateFieldName(string attribute)
        {
            var index = Array.IndexOf(JobFilter.DateAttributes, attribute);
            return DateFields[index < 0 ? 0 : index].ToLowerInvariant();
        }
        public static string PeriodHint => T("Untick a date to remove that bound", "Décochez une date pour retirer cette borne");
        public static string DateTip => T("Picking a date switches the period to Custom. Untick it for no bound (end date unticked = now).",
                                          "Choisir une date passe la période en Personnalisée. Décochez-la pour ne pas borner (date de fin décochée = maintenant).");
        public static string InvalidPeriod => T("The start date must be before the end date.", "La date de début doit précéder la date de fin.");
        public static string TypeLabel => "Type";
        public static string TableLabel => T("Table", "Table");
        public static string TableCue => T("All tables — pick or type", "Toutes les tables — choisir ou saisir");
        public static string TableTip => T("Regarding table: pick it in the list or type its name (empty = every table). The list is loaded with the first analysis.",
                                           "Table concernée : choisissez-la dans la liste ou saisissez son nom (vide = toutes les tables). La liste est chargée à la première analyse.");
        public static string NameLabel => T("Name", "Nom");
        public static string NameCue => T("contains…", "contient…");
        public static string NameTip => T("Text contained in the job name: workflow, plug-in step, flow… (optional)",
                                          "Texte contenu dans le nom du job : workflow, étape de plug-in, flux… (facultatif)");
        public static string MaxLabel => T("Max", "Max");
        public static string MaxTip => T("Maximum number of jobs loaded for the detailed analysis (latest first). Server counts are not limited.",
                                         "Nombre maximal de jobs chargés pour l'analyse détaillée (les plus récents d'abord). Les compteurs serveur ne sont pas limités.");
        public static string StatusesLabel => T("Statuses", "Statuts");
        public static string ProblemsOnly => T("Problems", "Problèmes");
        public static string ProblemsTip => T("Waiting for resources, Suspended and Failed", "En attente de ressources, Suspendu et Échec");
        public static string AllStatuses => T("All", "Tous");
        public static string NoStatusSelected => T("Select at least one status.", "Sélectionnez au moins un statut.");

        public static string[] Periods => IsFrench
            ? new[] { "Dernières 24 heures", "7 derniers jours", "30 derniers jours", "90 derniers jours", "12 derniers mois", "Tout", "Personnalisée" }
            : new[] { "Last 24 hours", "Last 7 days", "Last 30 days", "Last 90 days", "Last 12 months", "All time", "Custom" };

        /// <summary>Same order as <see cref="JobTypeFilter"/>.</summary>
        public static string[] JobTypes => IsFrench
            ? new[] { "Tous les types", "Événement système (plug-ins async)", "Workflow", "Suppression en bloc", "Importation", "Champs cumulatifs", "Autres types" }
            : new[] { "All types", "System event (async plug-ins)", "Workflow", "Bulk delete", "Import", "Rollup fields", "Other types" };

        public static string UnknownTable(string table) => T($"The table \"{table}\" does not exist (use its logical name).",
                                                             $"La table « {table} » n'existe pas (utilisez son nom logique).");

        #endregion

        #region Statuses / types

        public static string StatusWaitingForResources => T("Waiting for resources", "En attente de ressources");
        public static string StatusSuspended => T("Suspended (waiting)", "Suspendu (en attente)");
        public static string StatusInProgress => T("In progress", "En cours");
        public static string StatusPausing => T("Pausing", "Mise en pause");
        public static string StatusCanceling => T("Canceling", "Annulation");
        public static string StatusSucceeded => T("Succeeded", "Réussi");
        public static string StatusFailed => T("Failed", "Échec");
        public static string StatusCanceled => T("Canceled", "Annulé");

        public static string OperationType(int type)
        {
            switch (type)
            {
                case 1: return T("System Event", "Événement système");
                case 10: return "Workflow";
                case 13: return T("Bulk Delete", "Suppression en bloc");
                case 5: return T("Import", "Importation");
                case 57: return T("Calculate Rollup Field", "Calcul de champ cumulatif");
                default: return T($"Operation {type}", $"Opération {type}");
            }
        }

        public static string ProcessKindLabel(ProcessKind kind)
        {
            switch (kind)
            {
                case ProcessKind.Workflow: return T("Workflow", "Workflow");
                case ProcessKind.PluginStep: return T("Plug-in step", "Étape de plug-in");
                default: return T("System", "Système");
            }
        }

        public static string NoTable => T("(no regarding record)", "(aucun enregistrement)");
        public static string NoErrorMessage => T("(no error message)", "(aucun message d'erreur)");
        public static string ErrorWithCode(string code) => T($"Error {code} (no message)", $"Erreur {code} (sans message)");

        #endregion

        #region Tabs / progress

        public static string TabDashboard => T("Dashboard", "Tableau de bord");
        public static string TabErrors => T("Errors", "Erreurs");
        public static string TabJobs => "Jobs";
        public static string TabStorage => T("Storage cleanup", "Nettoyage du stockage");

        public static string NotAnalyzed => T("Set the scope, then click Analyze", "Définissez le périmètre, puis cliquez sur Analyser");
        public static string ResultInfo(int count, DateTime when) => T($"{count:N0} jobs loaded (latest first) · analyzed at {when:HH:mm}",
                                                                        $"{count:N0} jobs chargés (les plus récents) · analysés à {when:HH:mm}");
        public static string Truncated(int max) => T($"Limit of {max:N0} jobs reached: the oldest jobs of the scope are not loaded",
                                                     $"Limite de {max:N0} jobs atteinte : les jobs les plus anciens du périmètre ne sont pas chargés");

        public static string LoadingJobs => T("Loading system jobs…", "Chargement des System Jobs…");
        public static string LoadedJobs(int count) => T($"{count:N0} jobs loaded so far", $"{count:N0} jobs chargés pour l'instant");
        public static string CountingOnServer => T("Counting jobs on the server…", "Comptage des jobs sur le serveur…");
        public static string CountingJobs(long count) => T($"{count:N0} jobs counted so far (parallel aggregate queries by time slice)", $"{count:N0} jobs comptés pour l'instant (requêtes d'agrégat parallèles par tranche de temps)");
        public static string ReadingTableNames => T("Reading table names…", "Lecture des noms de tables…");
        public static string Analyzing => T("Grouping errors…", "Regroupement des erreurs…");
        public static string Working => T("Working…", "Traitement…");
        public static string Stop => T("Stop", "Arrêter");
        public static string Stopping => T("Stopping…", "Arrêt en cours…");
        public static string ElapsedFormat => T("Elapsed {0}", "Écoulé {0}");
        public static string LongRunning => T("Taking long? Stop it (or press Esc), then narrow the period or the statuses.",
                                              "Ça prend du temps ? Arrêtez (ou Échap), puis réduisez la période ou les statuts.");
        public static string TaskStopped => T("⏹ Stopped by the user", "⏹ Arrêté par l'utilisateur");
        public static string StoppedInfo => T("⏹ Loading stopped: partial analysis", "⏹ Chargement arrêté : analyse partielle");
        public static string CountsSkipped => T("Server counts skipped (loading stopped): the tiles show the loaded jobs only.",
                                                "Compteurs serveur ignorés (chargement arrêté) : les tuiles ne reflètent que les jobs chargés.");
        public static string AnalysisFailed => T("The analysis failed", "L'analyse a échoué");
        public static string NoData => T("No data for this scope", "Aucune donnée pour ce périmètre");
        public static string Now => T("now", "maintenant");

        #endregion

        #region Dashboard

        public static string TileScope => T("Jobs in scope (server)", "Jobs du périmètre (serveur)");
        public static string TileRunning => T("In progress / locked", "En cours / verrouillés");
        public static string CountsNote(int queries, bool approximate) => approximate
            ? T($"Server counts for the period, type, table and name, every status ({queries} queries). Some slices exceeded 50,000 jobs in under a minute: totals are lower bounds (≥).",
                $"Compteurs serveur pour la période, le type, la table et le nom, tous statuts ({queries} requêtes). Certaines tranches dépassaient 50 000 jobs en moins d'une minute : les totaux sont des minimums (≥).")
            : T($"Exact server counts for the period, type, table and name, every status ({queries} {(queries > 1 ? "queries" : "query")}) — click a tile to list the loaded jobs.",
                $"Compteurs serveur exacts pour la période, le type, la table et le nom, tous statuts ({queries} requête{(queries > 1 ? "s" : string.Empty)}) — cliquez sur une tuile pour lister les jobs chargés.");
        public static string CountsUnavailable => T("Server counts unavailable: the tiles show the loaded jobs only.", "Compteurs serveur indisponibles : les tuiles ne reflètent que les jobs chargés.");
        public static string CountsStale => T("Server counts were computed before the last action: click Analyze to refresh them.",
                                              "Les compteurs serveur datent d'avant la dernière action : cliquez sur Analyser pour les actualiser.");
        public static string TimelineTitle => T("Jobs over time", "Jobs dans le temps");
        public static string TimelineSubtitle => T("Loaded jobs per day of creation, by status", "Jobs chargés par jour de création, par statut");
        public static string TimelineSubtitleWeekly => T("Loaded jobs per week of creation, by status", "Jobs chargés par semaine de création, par statut");
        public static string SeriesWaiting => T("Waiting / in progress", "En attente / en cours");
        public static string ByStatusTitle => T("By status", "Par statut");
        public static string ByTypeTitle => T("By type", "Par type");
        public static string ByTableTitle => T("By table", "Par table");
        public static string ByProcessTitle => T("By process (top 15)", "Par processus (top 15)");
        public static string ClickToFilter => T("Loaded jobs — click a bar to list them", "Jobs chargés — cliquez sur une barre pour les lister");

        #endregion

        #region Errors

        public static string GroupsTitle => T("Error groups", "Groupes d'erreurs");
        public static string GroupsSubtitle(int jobs, int groups) => T($"{jobs:N0} failed or suspended jobs = {groups:N0} distinct {(groups > 1 ? "causes" : "cause")} (GUIDs, dates, numbers and names removed)",
                                                                        $"{jobs:N0} jobs en échec ou suspendus = {groups:N0} cause{(groups > 1 ? "s" : string.Empty)} distincte{(groups > 1 ? "s" : string.Empty)} (GUID, dates, nombres et noms retirés)");
        public static string NoErrors => T("No failed or suspended job in the loaded jobs", "Aucun job en échec ou suspendu parmi les jobs chargés");
        public static string TopFailingTitle => T("Top 10 failing processes", "Top 10 des processus qui échouent");
        public static string TopFailingSubtitle => T("Failed + suspended jobs, with the failure rate of each process", "Jobs en échec + suspendus, avec le taux d'échec de chaque processus");
        public static string GroupDetailTitle => T("Selected error", "Erreur sélectionnée");
        public static string GroupDetailSubtitle => T("Full message of the most recent job of the group", "Message complet du job le plus récent du groupe");
        public static string GroupInfo(ErrorGroup group) => T(
            $"{group.Count:N0} jobs ({group.FailedCount:N0} failed, {group.SuspendedCount:N0} suspended) · {group.ProcessCount} process(es) · {group.TableCount} table(s){(group.ErrorCodeHex.Length > 0 ? " · " + group.ErrorCodeHex : string.Empty)}\r\nFrom {SystemJobsDoctorControl.FormatDate(group.FirstSeen)} to {SystemJobsDoctorControl.FormatDate(group.LastSeen)} · main process: {group.TopProcess}",
            $"{group.Count:N0} jobs ({group.FailedCount:N0} en échec, {group.SuspendedCount:N0} suspendus) · {group.ProcessCount} processus · {group.TableCount} table(s){(group.ErrorCodeHex.Length > 0 ? " · " + group.ErrorCodeHex : string.Empty)}\r\nDu {SystemJobsDoctorControl.FormatDate(group.FirstSeen)} au {SystemJobsDoctorControl.FormatDate(group.LastSeen)} · processus principal : {group.TopProcess}");
        public static string ShowJobs => T("Show jobs", "Voir les jobs");
        public static string ResumeAll => T("Resume the suspended and failed jobs of this error group", "Reprend les jobs suspendus et en échec de ce groupe d'erreurs");
        public static string CancelAll => T("Cancel the jobs of this group that are not completed", "Annule les jobs non terminés de ce groupe");
        public static string DeleteAll => T("Delete the completed jobs of this group", "Supprime les jobs terminés de ce groupe");
        public static string Copy => T("Copy", "Copier");
        public static string CopyMessage => T("Copy the full message", "Copier le message complet");
        public static string CopyDetails => T("Copy the job details and message", "Copier le détail et le message du job");
        public static string ErrorFilter(string signature) => T("Error: ", "Erreur : ") + signature;
        public static string SameErrorFilter => T("Same error", "Même erreur");
        public static string ProblemsOf(string process) => T("Problems: ", "Problèmes : ") + process;
        public static string FailureRate(double rate) => T($"{rate:P0} failing", $"{rate:P0} en échec");

        public static string ColCount => T("Jobs", "Jobs");
        public static string ColSignature => T("Error (normalized)", "Erreur (normalisée)");
        public static string ColTopProcess => T("Main process", "Processus principal");
        public static string ColProcesses => T("Processes", "Processus");
        public static string ColTables => T("Tables", "Tables");
        public static string ColFirstSeen => T("First seen", "Première occurrence");
        public static string ColLastSeen => T("Last seen", "Dernière occurrence");
        public static string ColSampleMessage => T("Sample message", "Exemple de message");

        #endregion

        #region Jobs

        public static string JobsTitle => T("System jobs", "System Jobs");
        public static string JobsSubtitle => T("Ctrl / Shift to select several jobs, Ctrl+A for all, right click for actions",
                                               "Ctrl / Maj pour sélectionner plusieurs jobs, Ctrl+A pour tout, clic droit pour les actions");
        public static string JobDetailTitle => T("Job details", "Détail du job");
        public static string JobDetailSubtitle => T("Regarding record, process, owner, error and plug-in trace logs", "Enregistrement concerné, processus, propriétaire, erreur et Plugin Trace Log");
        public static string AllTables(int count) => T($"All tables — {count:N0}", $"Toutes les tables — {count:N0}");
        public static string JobTableTip => T("Only show the jobs of one table (tables of the loaded jobs, with their count)",
                                              "N'afficher que les jobs d'une table (tables des jobs chargés, avec leur nombre)");
        public static string SearchJobsCue => T("Search name, record, owner, error, id…", "Rechercher nom, enregistrement, propriétaire, erreur, id…");
        public static string JobCount(int visible, int total) => visible == total
            ? T($"{total:N0} jobs", $"{total:N0} jobs")
            : T($"{visible:N0} of {total:N0} jobs", $"{visible:N0} sur {total:N0} jobs");

        public static string ColStatus => T("Status", "Statut");
        public static string ColName => T("Name / process", "Nom / processus");
        public static string ColType => "Type";
        public static string ColTable => "Table";
        public static string ColRecord => T("Regarding record", "Enregistrement concerné");
        public static string ColRecordId => T("Record id", "Id de l'enregistrement");
        public static string ColOwner => T("Owner", "Propriétaire");
        public static string ColCreated => T("Created on", "Créé le");
        public static string ColCompleted => T("Completed on", "Terminé le");
        public static string ColPostponeUntil => T("Postponed until", "Reporté au");
        public static string ColRetries => T("Retries", "Essais");
        public static string ColError => T("Error", "Erreur");

        public static string DetailProcess => T("Process", "Processus");
        public static string DetailProcessKind => T("Process kind", "Type de processus");
        public static string DetailMessage => T("Message", "Message");
        public static string DetailStarted => T("Started on", "Démarré le");
        public static string DetailDuration => T("Duration", "Durée");
        public static string DetailDepth => T("Depth", "Profondeur");
        public static string DetailErrorCode => T("Error code", "Code d'erreur");
        public static string DetailCorrelation => T("Correlation id", "Id de corrélation");
        public static string DetailJobId => T("System job id", "Id du System Job");
        public static string NoMessage => T("(no message)", "(aucun message)");

        public static string Resume => T("Resume", "Reprendre");
        public static string Cancel => T("Cancel", "Annuler");
        public static string Postpone => T("Postpone", "Reporter");
        public static string Delete => T("Delete", "Supprimer");
        public static string ResumeTip => T("Resume the selected suspended or failed jobs", "Reprend les jobs suspendus ou en échec sélectionnés");
        public static string CancelTip => T("Cancel the selected jobs that are not completed", "Annule les jobs sélectionnés non terminés");
        public static string PostponeTip => T("Postpone the selected waiting or suspended jobs to a date", "Reporte à une date les jobs sélectionnés en attente ou suspendus");
        public static string DeleteTip => T("Delete the selected completed jobs (succeeded, failed or canceled)", "Supprime les jobs terminés sélectionnés (réussis, en échec ou annulés)");
        public static string OpenRecord => T("Open record", "Ouvrir l'enregistrement");
        public static string OpenJob => T("Open system job", "Ouvrir le System Job");
        public static string OpenProcess => T("Open process", "Ouvrir le processus");
        public static string TraceLogs => T("Plug-in trace logs", "Plugin Trace Log");
        public static string TraceLogsTip => T("Plug-in trace logs with the same correlation id as the job", "Traces de plug-in ayant le même id de corrélation que le job");
        public static string SameError => T("Show jobs with the same error", "Voir les jobs avec la même erreur");
        public static string SameProcess => T("Show jobs of the same process", "Voir les jobs du même processus");
        public static string CopyError => T("Copy error message", "Copier le message d'erreur");
        public static string CopyIds => T("Copy job ids", "Copier les id des jobs");
        public static string NoUrl => T("No link available (organization URL unknown).", "Aucun lien disponible (URL de l'organisation inconnue).");

        #endregion

        #region Actions

        public static string ActionTitle(JobActionKind kind)
        {
            switch (kind)
            {
                case JobActionKind.Resume: return T("Resume system jobs", "Reprendre des System Jobs");
                case JobActionKind.Cancel: return T("Cancel system jobs", "Annuler des System Jobs");
                case JobActionKind.Postpone: return T("Postpone system jobs", "Reporter des System Jobs");
                default: return T("Delete system jobs", "Supprimer des System Jobs");
            }
        }

        public static string ActionSubtitle(JobActionKind kind)
        {
            switch (kind)
            {
                case JobActionKind.Resume: return T("Suspended and failed jobs go back to the queue (Waiting for resources), like the Resume button of Dynamics 365",
                                                    "Les jobs suspendus et en échec retournent dans la file (En attente de ressources), comme le bouton Reprendre de Dynamics 365");
                case JobActionKind.Cancel: return T("Jobs are stopped and marked as Canceled", "Les jobs sont arrêtés et passent au statut Annulé");
                case JobActionKind.Postpone: return T("Jobs will run at the chosen date", "Les jobs s'exécuteront à la date choisie");
                default: return T("Permanent deletion of completed jobs: frees storage", "Suppression définitive des jobs terminés : libère du stockage");
            }
        }

        public static string ActionRule(JobActionKind kind)
        {
            switch (kind)
            {
                case JobActionKind.Resume: return T("Suspended and failed jobs can be resumed; the others are skipped. The state of each failed job is checked afterwards. Fix the cause first, or they will fail again.",
                                                    "Les jobs suspendus et en échec peuvent être repris ; les autres sont ignorés. L'état de chaque job en échec est vérifié ensuite. Corrigez d'abord la cause, sinon ils échoueront à nouveau.");
                case JobActionKind.Cancel: return T("Completed jobs are skipped. A job that is currently running may refuse to be canceled.",
                                                    "Les jobs terminés sont ignorés. Un job en cours d'exécution peut refuser l'annulation.");
                case JobActionKind.Postpone: return T("Only waiting and suspended jobs can be postponed. Suspended jobs are put back in the queue so they run at the new date.",
                                                      "Seuls les jobs en attente et suspendus peuvent être reportés. Les jobs suspendus sont remis dans la file pour s'exécuter à la nouvelle date.");
                default: return T("Only completed jobs (succeeded, failed, canceled) can be deleted; cancel the others first. Deleted jobs cannot be restored.",
                                  "Seuls les jobs terminés (réussis, en échec, annulés) peuvent être supprimés ; annulez d'abord les autres. Un job supprimé ne peut pas être restauré.");
            }
        }

        public static string TileEligible(JobActionKind kind)
        {
            switch (kind)
            {
                case JobActionKind.Resume: return T("Jobs to resume", "Jobs à reprendre");
                case JobActionKind.Cancel: return T("Jobs to cancel", "Jobs à annuler");
                case JobActionKind.Postpone: return T("Jobs to postpone", "Jobs à reporter");
                default: return T("Jobs to delete", "Jobs à supprimer");
            }
        }

        public static string TileFailedToResume => T("Failed jobs among them", "Dont jobs en échec");
        public static string RerunOption => T("Run the workflow again if a failed job cannot be resumed", "Relancer le workflow si un job en échec ne peut pas être repris");
        public static string RerunOptionTip => T("Dataverse can refuse to resume a completed (failed) job. The workflow is then run again on the same record: a new system job is created, the failed one stays (delete it later).",
                                                 "Dataverse peut refuser de reprendre un job terminé (en échec). Le workflow est alors relancé sur le même enregistrement : un nouveau System Job est créé, l'ancien reste (supprimez-le ensuite).");
        public static string CheckingResumes => T("Checking that the failed jobs restarted…", "Vérification de la reprise des jobs en échec…");
        public static string ResumeRefused => T("The platform did not restart this failed job (it stays completed).", "La plateforme n'a pas relancé ce job en échec (il reste terminé).");
        public static string ResumeRefusedNotWorkflow(string reason) => reason + " " + T("Only workflows can be run again: trigger the operation again on the record (plug-in, flow…).",
                                                                                      "Seuls les workflows peuvent être relancés : redéclenchez l'opération sur l'enregistrement (plug-in, flux…).");
        public static string RerunFailed(string message) => T("Workflow could not be run again: ", "Impossible de relancer le workflow : ") + message;
        public static string Rerunning(int done, int total) => T($"Running the workflows again… {done:N0} / {total:N0}", $"Relance des workflows… {done:N0} / {total:N0}");
        public static string TileSkipped => T("Skipped (not eligible)", "Ignorés (non éligibles)");
        public static string ImpactedProcesses => T("PROCESSES CONCERNED", "PROCESSUS CONCERNÉS");
        public static string DeleteAcknowledge => T("I understand the deletion is permanent", "Je comprends que la suppression est définitive");
        public static string CancelButton => T("Cancel", "Annuler");
        public static string Continue => T("Continue", "Continuer");
        public static string Save => T("Save", "Enregistrer");
        public static string PostponeTo(DateTime local) => T($"New date: {local:yyyy-MM-dd HH:mm}", $"Nouvelle date : {local:yyyy-MM-dd HH:mm}");

        public static string ActionButton(JobActionKind kind, int count)
        {
            switch (kind)
            {
                case JobActionKind.Resume: return T($"Resume {count:N0} jobs", $"Reprendre {count:N0} jobs");
                case JobActionKind.Cancel: return T($"Cancel {count:N0} jobs", $"Annuler {count:N0} jobs");
                case JobActionKind.Postpone: return T($"Postpone {count:N0} jobs", $"Reporter {count:N0} jobs");
                default: return T($"Delete {count:N0} jobs", $"Supprimer {count:N0} jobs");
            }
        }

        public static IconKind ActionGlyph(JobActionKind kind)
        {
            switch (kind)
            {
                case JobActionKind.Resume: return IconKind.Play;
                case JobActionKind.Cancel: return IconKind.Stop;
                case JobActionKind.Postpone: return IconKind.Clock;
                default: return IconKind.Trash;
            }
        }

        public static string NothingEligible(JobActionKind kind) => T("None of the selected jobs can be processed by this action.", "Aucun des jobs sélectionnés ne peut être traité par cette action.")
                                                                    + Environment.NewLine + Environment.NewLine + ActionRule(kind);

        public static string ActionProgress(JobActionKind kind, int done, int total)
        {
            string verb;
            switch (kind)
            {
                case JobActionKind.Resume: verb = T("Resuming", "Reprise"); break;
                case JobActionKind.Cancel: verb = T("Canceling", "Annulation"); break;
                case JobActionKind.Postpone: verb = T("Postponing", "Report"); break;
                default: verb = T("Deleting", "Suppression"); break;
            }
            return $"{verb}… {done:N0} / {total:N0}";
        }

        public static string ActionFailed => T("The action failed", "L'action a échoué");

        public static string ActionSummary(JobActionResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(T($"{result.Succeeded.Count:N0} jobs processed successfully.", $"{result.Succeeded.Count:N0} jobs traités avec succès."));
            if (result.Rerun.Count > 0)
            {
                builder.AppendLine(T($"{result.Rerun.Count:N0} failed workflow jobs could not be resumed: their workflow was run again on the record (new system jobs). Click Analyze to see them.",
                                     $"{result.Rerun.Count:N0} jobs de workflow en échec n'ont pas pu être repris : leur workflow a été relancé sur l'enregistrement (nouveaux System Jobs). Cliquez sur Analyser pour les voir."));
            }
            if (result.Canceled) builder.AppendLine(T("The action was stopped before the end.", "L'action a été interrompue avant la fin."));
            if (result.Failed.Count > 0)
            {
                builder.AppendLine(T($"{result.Failed.Count:N0} jobs failed:", $"{result.Failed.Count:N0} jobs en erreur :"));
                foreach (var error in result.Failed.GroupBy(f => f.Value).OrderByDescending(g => g.Count()).Take(5))
                {
                    var message = error.Key ?? string.Empty;
                    if (message.Length > 200) message = message.Substring(0, 200) + "…";
                    builder.AppendLine($"  • {error.Count():N0} × {message}");
                }
                builder.AppendLine(T("Details are in the XrmToolBox logs.", "Le détail figure dans les logs XrmToolBox."));
            }
            return builder.ToString().TrimEnd();
        }

        public static string PostponeTitle => T("Postpone system jobs", "Reporter des System Jobs");
        public static string PostponeSubtitle(int count) => T($"{count:N0} waiting or suspended jobs will run at this date", $"{count:N0} jobs en attente ou suspendus s'exécuteront à cette date");
        public static string PostponeUntilLabel => T("Run on", "Exécuter le");
        public static string PresetOneHour => T("In 1 hour", "Dans 1 heure");
        public static string PresetTonight => T("Tonight 22:00", "Ce soir 22:00");
        public static string PresetTomorrow => T("Tomorrow 06:00", "Demain 06:00");
        public static string PresetWeekend => T("Saturday 02:00", "Samedi 02:00");
        public static string PostponeNote => T("Useful to move heavy jobs out of business hours, or to let a fix be deployed before they run again.",
                                               "Utile pour déplacer des jobs lourds hors des heures ouvrées, ou laisser le temps de déployer un correctif avant leur nouvelle exécution.");
        public static string PostponeInPast => T("Choose a date in the future.", "Choisissez une date dans le futur.");

        #endregion

        #region Trace logs

        public static string LoadingTraces => T("Loading plug-in trace logs…", "Chargement des Plugin Trace Log…");
        public static string TracesFailed => T("Unable to read the plug-in trace logs", "Impossible de lire les Plugin Trace Log");
        public static string TraceSubtitle(string process, int count) => T($"{process} · {count} trace(s) with the same correlation id",
                                                                           $"{process} · {count} trace(s) avec le même id de corrélation");
        public static string TraceDisabled => T("Plug-in trace logging is disabled in this organization (System Settings > Customization > Enable logging to plug-in trace log).",
                                                "La journalisation des traces de plug-in est désactivée dans cette organisation (Paramètres système > Personnalisation > Activer la journalisation dans le journal de suivi de plug-in).");
        public static string NoTraces => T("No trace found for this job. Traces are kept for 24 hours and only written when logging is set to Exception or All.",
                                           "Aucune trace trouvée pour ce job. Les traces sont conservées 24 heures et ne sont écrites que si la journalisation est sur Exception ou Tout.");
        public static string TraceType => T("Plug-in type", "Type de plug-in");
        public static string TraceMode => T("Mode", "Mode");
        public static string TraceDuration => T("Duration", "Durée");
        public static string TraceException => T("Exception", "Exception");
        public static string TraceMessageBlock => T("Trace (message block)", "Trace (bloc de message)");
        public static string OpenTraceList => T("Open trace log list", "Ouvrir la liste des traces");

        #endregion

        #region Storage

        public static string TableSizeTitle => T("System jobs table", "Table des System Jobs");
        public static string TableSizeSubtitle => T("asyncoperation is often one of the biggest tables of the database", "asyncoperation est souvent l'une des plus grosses tables de la base");
        public static string TileSnapshot => T("Rows (platform snapshot)", "Lignes (instantané plateforme)");
        public static string TileRows => T("Rows (counted)", "Lignes (comptées)");
        public static string TileTableSize => T("Estimated size", "Taille estimée");
        public static string AvgKbLabel => T("Avg KB / job", "Ko moyen / job");
        public static string AvgKbTip => T("Average size of one system job, used for the storage estimates. Jobs with large contexts or messages weigh more.",
                                           "Taille moyenne d'un System Job, utilisée pour les estimations de stockage. Les jobs avec un contexte ou un message volumineux pèsent davantage.");
        public static string AdminMbLabel => T("Admin center size (MB)", "Taille centre d'admin (Mo)");
        public static string AdminMbTip => T("Optional: size of AsyncOperationBase shown in the Power Platform admin center (Capacity > Database). When set, the average size per job is computed from it.",
                                             "Facultatif : taille d'AsyncOperationBase affichée dans le centre d'administration Power Platform (Capacité > Base de données). Si elle est renseignée, la taille moyenne par job en est déduite.");
        public static string Refresh => T("Refresh", "Actualiser");
        public static string CountAll => T("Count by status", "Compter par statut");
        public static string CountAllTip => T("Exact count of the whole table per status (several aggregate queries on large tables)",
                                              "Comptage exact de toute la table par statut (plusieurs requêtes d'agrégat sur les grosses tables)");
        public static string CountAllHint => T("Click « Count by status » to count the whole table", "Cliquez sur « Compter par statut » pour compter toute la table");
        public static string StorageNote => T("The row snapshot is refreshed by Dataverse every few hours. For the exact size, read AsyncOperationBase in the admin center and enter it above.",
                                              "L'instantané des lignes est actualisé par Dataverse toutes les quelques heures. Pour la taille exacte, relevez AsyncOperationBase dans le centre d'administration et saisissez-la ci-dessus.");
        public static string TableStatusesTitle => T("Whole table by status", "Toute la table par statut");
        public static string TableStatusesSubtitle => T("Every system job of the organization, all dates", "Tous les System Jobs de l'organisation, toutes dates");
        public static string TableStatusesDone(long total, int queries, bool approximate) => T(
            $"{(approximate ? "≥ " : string.Empty)}{total:N0} jobs, all dates ({queries} queries)",
            $"{(approximate ? "≥ " : string.Empty)}{total:N0} jobs, toutes dates ({queries} requêtes)");

        public static string RuleTitle => T("Cleanup rule", "Règle de nettoyage");
        public static string RuleSubtitle => T("Completed jobs to delete — estimate before creating the job", "Jobs terminés à supprimer — estimez avant de créer le job");
        public static string DeleteJobsLabel => T("Delete", "Supprimer");
        public static string OlderThanLabel => T("Completed more than (days)", "Terminés depuis plus de (jours)");
        public static string Estimate => T("Estimate", "Estimer");
        public static string PreviewQuery => T("Query", "Requête");
        public static string PreviewQueryTip => T("FetchXML of the bulk deletion job", "FetchXML du job de suppression en bloc");
        public static string PreviewQueryTitle => T("Bulk deletion query", "Requête de suppression en bloc");
        public static string PreviewQuerySubtitle => T("System jobs matched by the cleanup rule (relative date: works for recurring jobs)",
                                                       "System Jobs ciblés par la règle de nettoyage (date relative : fonctionne pour les jobs récurrents)");
        public static string TileCleanCount => T("Jobs to delete today", "Jobs à supprimer aujourd'hui");
        public static string TileCleanSpace => T("Storage freed", "Stockage libéré");
        public static string RuleNote => T("Microsoft recommends deleting succeeded and canceled system jobs regularly. Waiting, suspended and running jobs are never deleted by this rule.",
                                           "Microsoft recommande de supprimer régulièrement les System Jobs réussis et annulés. Les jobs en attente, suspendus ou en cours ne sont jamais supprimés par cette règle.");
        public static string EstimateNote(double averageKb, double? share) => T(
            $"Estimate based on {averageKb:N1} KB per job{(share.HasValue ? $" · {share.Value:P0} of the table" : string.Empty)}. Dataverse may take a few hours to report the freed capacity.",
            $"Estimation basée sur {averageKb:N1} Ko par job{(share.HasValue ? $" · {share.Value:P0} de la table" : string.Empty)}. Dataverse peut mettre quelques heures à refléter la capacité libérée.");
        public static string NoCleanupStatus => T("Select at least one status to delete.", "Sélectionnez au moins un statut à supprimer.");

        public static string BulkTitle => T("Bulk deletion job", "Job de suppression en bloc");
        public static string BulkSubtitle => T("Scheduled by Dataverse itself (Settings > Data management > Bulk record deletion)",
                                               "Planifié par Dataverse lui-même (Paramètres > Gestion des données > Suppression d'enregistrements en bloc)");
        public static string JobNameLabel => T("Name", "Nom");
        public static string StartLabel => T("Start", "Début");
        public static string RecurrenceLabel => T("Repeat", "Répéter");
        public static string[] RecurrenceOptions => IsFrench
            ? new[] { "Une seule fois", "Tous les jours", "Toutes les semaines", "Tous les mois" }
            : new[] { "Once", "Every day", "Every week", "Every month" };
        public static string CreateBulk => T("Create bulk deletion job", "Créer le job de suppression");
        public static string CreatingBulk => T("Creating the bulk deletion job…", "Création du job de suppression en bloc…");
        public static string BulkFailed => T("Unable to create the bulk deletion job", "Impossible de créer le job de suppression en bloc");
        public static string BulkCreated(string name, DateTime start) => T($"✔ « {name} » scheduled on {start:yyyy-MM-dd HH:mm}", $"✔ « {name} » planifié le {start:yyyy-MM-dd HH:mm}");
        public static string OpenCreatedJob => T("Open the job", "Ouvrir le job");
        public static string ExistingBulkJobs => T("BULK DELETION JOBS ON SYSTEM JOBS (double-click to open)", "JOBS DE SUPPRESSION EN BLOC SUR LES SYSTEM JOBS (double-clic pour ouvrir)");
        public static string NoBulkJobs => T("NO BULK DELETION JOB ON SYSTEM JOBS YET", "AUCUN JOB DE SUPPRESSION EN BLOC SUR LES SYSTEM JOBS POUR L'INSTANT");
        public static string DefaultBulkJobName(int days) => T($"System Jobs Doctor - delete completed system jobs older than {days} days",
                                                               $"System Jobs Doctor - suppression des System Jobs terminés depuis plus de {days} jours");
        public static string LoadingStorage => T("Reading the system jobs table…", "Lecture de la table des System Jobs…");
        public static string StorageFailed => T("Unable to read the storage information", "Impossible de lire les informations de stockage");
        public static string CountFailed => T("The count failed", "Le comptage a échoué");

        public static string ConfirmBulkDelete(string name, CleanupRule rule, DateTime start, string recurrence, long? count)
        {
            var statuses = string.Join(", ", rule.Statuses.Select(JobStatus.Label));
            var type = JobTypes[(int)rule.Type];
            var estimate = count.HasValue ? T($"\r\nEstimate today: {count.Value:N0} jobs.", $"\r\nEstimation aujourd'hui : {count.Value:N0} jobs.") : string.Empty;
            return T(
                $"Create the bulk deletion job « {name} »?\r\n\r\nDeletes the system jobs: {statuses}\r\nCompleted more than {rule.OlderThanDays} days ago · {type}\r\nStart: {start:yyyy-MM-dd HH:mm} · {recurrence}{estimate}\r\n\r\nDeleted system jobs cannot be restored.",
                $"Créer le job de suppression en bloc « {name} » ?\r\n\r\nSupprime les System Jobs : {statuses}\r\nTerminés depuis plus de {rule.OlderThanDays} jours · {type}\r\nDébut : {start:yyyy-MM-dd HH:mm} · {recurrence}{estimate}\r\n\r\nLes System Jobs supprimés ne peuvent pas être restaurés.");
        }

        public static string ColRecurring => T("Recurring", "Récurrent");
        public static string ColNextRun => T("Next run", "Prochaine exécution");
        public static string ColDeleted => T("Deleted", "Supprimés");
        public static string ColFailures => T("Failures", "Échecs");
        public static string Yes => T("Yes", "Oui");
        public static string No => T("No", "Non");

        #endregion

        #region Failed jobs summary

        public static string SummaryDialogTitle => T("Failed jobs summary", "Résumé des jobs en échec");
        public static string SummaryDialogSubtitle => T("Causes, processes, tables and next steps — copy it into an e-mail, a ticket or Teams",
                                                        "Causes, processus, tables et prochaines étapes — à copier dans un e-mail, un ticket ou Teams");
        public static string SummaryScopeAll(int count) => T($"All loaded jobs ({count:N0})", $"Tous les jobs chargés ({count:N0})");
        public static string SummaryScopeList(int count) => T($"Current job list ({count:N0})", $"Liste de jobs actuelle ({count:N0})");
        public static string SummaryIncludeSuspended => T("Include suspended jobs", "Inclure les jobs suspendus");
        public static string SummaryScopeAllLabel => T("all loaded jobs", "tous les jobs chargés");
        public static string SummaryScopeListLabel(string filters) => T($"job list filtered on {filters}", $"liste de jobs filtrée sur {filters}");
        public static string SummaryFileFilter => T("Text file (*.txt)|*.txt|Markdown (*.md)|*.md", "Fichier texte (*.txt)|*.txt|Markdown (*.md)|*.md");
        public static string SummarizeJob => T("Summarize this job", "Résumer ce job");
        public static string SummarizeJobs(int count) => T($"Summarize the {count:N0} selected jobs", $"Résumer les {count:N0} jobs sélectionnés");
        public static string SummarizeJobTip => T("Summary of the selected job: cause, hint, same error elsewhere, full message, links and next steps",
                                                  "Résumé du job sélectionné : cause, piste de résolution, même erreur ailleurs, message complet, liens et prochaines étapes");
        public static string SummaryScopeSelection(int count) => T($"Selected jobs ({count:N0})", $"Jobs sélectionnés ({count:N0})");
        public static string SummaryScopeSelectionLabel(int count) => T($"{count:N0} selected jobs", $"{count:N0} jobs sélectionnés");
        public static string JobSummaryTitle => T("Job summary", "Résumé du job");
        public static string JobSummarySubtitle(string name) => name;

        public static string JobSummaryHeader(int status, string name) => (status == JobStatus.Failed ? T("FAILED SYSTEM JOB", "SYSTEM JOB EN ÉCHEC")
            : status == JobStatus.Waiting ? T("SUSPENDED SYSTEM JOB", "SYSTEM JOB SUSPENDU")
            : T("SYSTEM JOB", "SYSTEM JOB")) + " — " + name;
        public static string JobSummaryWhat => T("WHAT", "QUOI");
        public static string JobSummaryCause => T("CAUSE", "CAUSE");
        public static string JobSummaryNoError => T("This job has no error message.", "Ce job n'a pas de message d'erreur.");
        public static string JobSummaryNormalized(string signature) => T("Normalized error: ", "Erreur normalisée : ") + signature;
        public static string JobSummarySameHeader => T("SAME ERROR IN THIS ANALYSIS", "MÊME ERREUR DANS CETTE ANALYSE");
        public static string JobSummaryAlone => T("This job is the only one with this error among the loaded jobs: probably an isolated case (data of this record).",
                                                  "Ce job est le seul à avoir cette erreur parmi les jobs chargés : probablement un cas isolé (données de cet enregistrement).");
        public static string JobSummarySame(int count, int failed, int suspended, int records, int processes) => T(
            $"{count:N0} jobs share this cause ({failed:N0} failed, {suspended:N0} suspended) on {records:N0} record(s) and {processes:N0} process(es): a systemic problem, not this record alone.",
            $"{count:N0} jobs partagent cette cause ({failed:N0} en échec, {suspended:N0} suspendus) sur {records:N0} enregistrement(s) et {processes:N0} processus : un problème systémique, pas seulement cet enregistrement.");
        public static string JobSummaryMessageHeader => T("FULL MESSAGE", "MESSAGE COMPLET");
        public static string JobSummaryTruncated(int lines) => T($"… ({lines:N0} more lines: see the job details or the Plug-in trace logs)", $"… ({lines:N0} lignes de plus : voir le détail du job ou le Plugin Trace Log)");
        public static string JobSummaryLinks => T("LINKS", "LIENS");
        public static string JobSummaryLinkJob => T("System job", "System Job");
        public static string JobSummaryLinkRecord => T("Record", "Enregistrement");
        public static string JobSummaryLinkProcess => T("Process", "Processus");
        public static string JobSummaryNextResume => T("The job is suspended: fix the cause, then Resume it (it restarts from the step that failed).",
                                                       "Le job est suspendu : corrigez la cause, puis Reprenez-le (il repart de l'étape en échec).");
        public static string JobSummaryNextRerun => T("A failed job is not retried automatically: once fixed, click Resume (a failed workflow is run again on this record if needed).",
                                                      "Un job en échec n'est pas relancé automatiquement : une fois corrigé, cliquez sur Reprendre (un workflow en échec est relancé sur cet enregistrement si besoin).");
        public static string JobSummaryNextTraces(string correlation) => T($"Plug-in: click « Plug-in trace logs » (correlation id {correlation}) to read the trace and the exception.",
                                                                           $"Plug-in : cliquez sur « Plugin Trace Log » (id de corrélation {correlation}) pour lire la trace et l'exception.");
        public static string JobSummaryNextSame => T("Use « Show jobs with the same error » (right click) to act on every job of this cause at once.",
                                                     "Utilisez « Voir les jobs avec la même erreur » (clic droit) pour agir sur tous les jobs de cette cause en une fois.");
        public static string JobSummaryRetries(int retries) => T($"Retried {retries} time(s) by the platform.", $"Relancé {retries} fois par la plateforme.");
        public static string SaveAs => T("Save as…", "Enregistrer sous…");
        public static string Copied => T("Copied ✔", "Copié ✔");

        public static string SummaryTitle(bool includeSuspended, string organization)
        {
            var title = includeSuspended ? T("FAILED AND SUSPENDED SYSTEM JOBS", "SYSTEM JOBS EN ÉCHEC ET SUSPENDUS") : T("FAILED SYSTEM JOBS", "SYSTEM JOBS EN ÉCHEC");
            return string.IsNullOrEmpty(organization) ? title : $"{title} — {organization}";
        }

        public static string SummaryContext(DateTime analyzedOn, JobFilter filter, string scope)
        {
            var from = filter.FromUtc.HasValue ? filter.FromUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "…";
            var to = filter.ToUtc.HasValue ? filter.ToUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : Now;
            var table = string.IsNullOrEmpty(filter.Table) ? string.Empty : T($" · table {filter.Table}", $" · table {filter.Table}");
            return T($"Analyzed on {analyzedOn:yyyy-MM-dd HH:mm} · period {from} → {to} ({DateFieldName(filter.DateAttribute)}){table} · {scope}",
                     $"Analysé le {analyzedOn:yyyy-MM-dd HH:mm} · période {from} → {to} ({DateFieldName(filter.DateAttribute)}){table} · {scope}");
        }

        public static string SummaryNoFailure(bool includeSuspended) => includeSuspended
            ? T("No failed or suspended job in this scope.", "Aucun job en échec ou suspendu dans ce périmètre.")
            : T("No failed job in this scope.", "Aucun job en échec dans ce périmètre.");

        public static string SummaryOverviewHeader => T("OVERVIEW", "VUE D'ENSEMBLE");

        public static string SummaryFailedCount(int total, int failed, int suspended, int loaded, bool includeSuspended)
        {
            var share = loaded == 0 ? 0 : (double)total / loaded;
            return includeSuspended
                ? T($"{total:N0} jobs in trouble ({failed:N0} failed, {suspended:N0} suspended) = {share:P1} of the {loaded:N0} jobs analyzed",
                    $"{total:N0} jobs en difficulté ({failed:N0} en échec, {suspended:N0} suspendus) = {share:P1} des {loaded:N0} jobs analysés")
                : T($"{total:N0} failed jobs = {share:P1} of the {loaded:N0} jobs analyzed",
                    $"{total:N0} jobs en échec = {share:P1} des {loaded:N0} jobs analysés");
        }

        public static string SummaryServerFailed(long count, bool approximate) => T(
            $"Server count for the whole period: {(approximate ? "≥ " : string.Empty)}{count:N0}",
            $"Compteur serveur sur toute la période : {(approximate ? "≥ " : string.Empty)}{count:N0}");

        public static string SummaryDistinct(int causes, int processes, int tables) => T(
            $"{causes:N0} distinct cause(s) · {processes:N0} process(es) · {tables:N0} table(s)",
            $"{causes:N0} cause(s) distincte(s) · {processes:N0} processus · {tables:N0} table(s)");

        public static string SummaryRange(DateTime? first, DateTime? last) => T(
            $"First: {SystemJobsDoctorControl.FormatDate(first)} · last: {SystemJobsDoctorControl.FormatDate(last)}",
            $"Premier : {SystemJobsDoctorControl.FormatDate(first)} · dernier : {SystemJobsDoctorControl.FormatDate(last)}");

        public static string SummaryPeak(DateTime day, int count) => T($"Worst day: {day:yyyy-MM-dd} ({count:N0} jobs)", $"Pire journée : {day:yyyy-MM-dd} ({count:N0} jobs)");

        public static string SummaryCausesHeader(int shown, int total) => shown == total
            ? T("CAUSES", "CAUSES")
            : T($"TOP {shown} CAUSES (of {total})", $"TOP {shown} DES CAUSES (sur {total})");

        public static string SummaryCauseLine(int count, double share) => T($"{count:N0} jobs ({share:P0})", $"{count:N0} jobs ({share:P0})");

        public static string SummaryProcess(string name, int others) => others > 0
            ? T($"Process: {name} (+{others})", $"Processus : {name} (+{others})")
            : T($"Process: {name}", $"Processus : {name}");

        public static string SummaryTables(string tables, int others) => T("Tables: ", "Tables : ") + tables + (others > 0 ? $" (+{others})" : string.Empty);

        public static string SummaryLast(DateTime? last) => T("Last: ", "Dernier : ") + SystemJobsDoctorControl.FormatDate(last);

        public static string SummaryOtherCauses(int causes, int jobs) => T($"… and {causes:N0} other cause(s) ({jobs:N0} jobs)", $"… et {causes:N0} autre(s) cause(s) ({jobs:N0} jobs)");

        public static string SummaryProcessesHeader => T("PROCESSES", "PROCESSUS");

        public static string SummaryProcessLine(int failed, int total, double rate) => T(
            $"{failed:N0} of {total:N0} runs ({rate:P0})",
            $"{failed:N0} sur {total:N0} exécutions ({rate:P0})");

        public static string SummaryMainCause(string cause) => T("Main cause: ", "Cause principale : ") + cause;

        public static string SummaryTablesHeader => T("TABLES", "TABLES");

        public static string SummaryNextHeader => T("NEXT STEPS", "PROCHAINES ÉTAPES");

        public static string SummaryNextTopCause(double share, int causes) => causes == 1
            ? T("A single cause explains every failure: fixing it solves the whole problem.", "Une seule cause explique tous les échecs : la corriger règle tout le problème.")
            : T($"Fix the first cause first: it explains {share:P0} of the failures.", $"Corrigez d'abord la première cause : elle explique {share:P0} des échecs.");

        public static string SummaryNextResume => T("Once the cause is fixed, resume the suspended jobs (Errors or Jobs page > Resume).",
                                                    "Une fois la cause corrigée, reprenez les jobs suspendus (page Erreurs ou Jobs > Reprendre).");
        public static string SummaryNextCleanup => T("Failed jobs are not retried automatically: once fixed, select them and click Resume (failed workflows are run again if needed), then delete the old failed jobs (Storage cleanup).",
                                                     "Les jobs en échec ne sont pas relancés automatiquement : une fois corrigés, sélectionnez-les et cliquez sur Reprendre (les workflows en échec sont relancés si besoin), puis supprimez les anciens échecs (Nettoyage du stockage).");
        public static string SummaryNextTraces => T("For plug-in failures, open a job and click « Plug-in trace logs » to read the trace and the full exception.",
                                                    "Pour les échecs de plug-in, ouvrez un job et cliquez sur « Plugin Trace Log » pour lire la trace et l'exception complète.");
        public static string SummaryNextTruncated(int max) => T($"Only the {max:N0} latest jobs were analyzed: narrow the period or raise the limit for a complete picture.",
                                                                $"Seuls les {max:N0} jobs les plus récents ont été analysés : réduisez la période ou augmentez la limite pour une vue complète.");

        public static string HintText(HintKind kind)
        {
            switch (kind)
            {
                case HintKind.RecordNotFound:
                    return T("The record (or a record it points to) was deleted or merged before the job ran. Make the process check that the record still exists.",
                             "L'enregistrement (ou un enregistrement lié) a été supprimé ou fusionné avant l'exécution du job. Faites vérifier au processus que l'enregistrement existe encore.");
                case HintKind.Privilege:
                    return T("Missing privilege: the job runs as its owner (workflow) or the calling user (plug-in). Check the security roles of that user.",
                             "Privilège manquant : le job s'exécute avec son propriétaire (workflow) ou l'utilisateur appelant (plug-in). Vérifiez les rôles de sécurité de cet utilisateur.");
                case HintKind.Timeout:
                    return T("Time limit reached (2 minutes for a plug-in). Optimize the code or the external calls, or split the work into smaller jobs.",
                             "Limite de temps atteinte (2 minutes pour un plug-in). Optimisez le code ou les appels externes, ou découpez le traitement.");
                case HintKind.Throttling:
                    return T("Service protection limits (API requests / execution time). Spread the load over time and add retries.",
                             "Limites de protection du service (requêtes API / temps d'exécution). Étalez la charge dans le temps et ajoutez des relances.");
                case HintKind.Sql:
                    return T("Database contention or SQL timeout, often caused by many jobs updating the same records at the same time. Usually transient: resume, then reduce concurrency.",
                             "Contention ou timeout SQL, souvent dû à de nombreux jobs modifiant les mêmes enregistrements en même temps. Généralement passager : reprenez, puis réduisez la concurrence.");
                case HintKind.NullReference:
                    return T("Bug in the plug-in code: a value is used without being checked (attribute missing from the Target or the image).",
                             "Bug dans le code du plug-in : une valeur est utilisée sans être vérifiée (attribut absent de la cible ou de l'image).");
                case HintKind.BusinessError:
                    return T("A plug-in deliberately blocked the operation (business validation). Check whether the data really breaks the rule.",
                             "Un plug-in a volontairement bloqué l'opération (règle métier). Vérifiez si les données enfreignent réellement la règle.");
                case HintKind.ExternalService:
                    return T("An external service called by the process is unavailable or refused the call. Check its availability, URL and credentials.",
                             "Un service externe appelé par le processus est indisponible ou a refusé l'appel. Vérifiez sa disponibilité, son URL et ses identifiants.");
                case HintKind.Duplicate:
                    return T("Duplicate: the record already exists (alternate key or duplicate detection). Make the process look the record up before creating it.",
                             "Doublon : l'enregistrement existe déjà (clé alternative ou détection des doublons). Faites rechercher l'enregistrement avant de le créer.");
                case HintKind.InfiniteLoop:
                    return T("Infinite loop detected: the process triggers itself (depth limit). Filter the trigger attributes or check the depth.",
                             "Boucle infinie détectée : le processus se déclenche lui-même (limite de profondeur). Filtrez les attributs déclencheurs ou testez la profondeur.");
                case HintKind.Schema:
                    return T("Schema mismatch: a column was renamed or deleted, or receives an invalid value. Update the process after the schema change.",
                             "Incohérence de schéma : une colonne a été renommée ou supprimée, ou reçoit une valeur invalide. Mettez à jour le processus.");
                case HintKind.DisabledUser:
                    return T("The owner of the process or record is disabled. Assign the process to an active user or an application user.",
                             "Le propriétaire du processus ou de l'enregistrement est désactivé. Attribuez le processus à un utilisateur actif ou à un utilisateur d'application.");
                case HintKind.ProcessDeactivated:
                    return T("The process (or a child process) is deactivated. Activate it again, or cancel the pending jobs.",
                             "Le processus (ou un processus enfant) est désactivé. Réactivez-le, ou annulez les jobs en attente.");
                default:
                    return string.Empty;
            }
        }

        #endregion

        #region Options

        public static string OptionMaxRecords => T("Jobs loaded per analysis", "Jobs chargés par analyse");
        public static string OptionMaxRecordsHelp => T("Latest jobs of the scope loaded for the dashboard, error groups and job list. Server counts are always exact.",
                                                       "Jobs les plus récents du périmètre chargés pour le tableau de bord, les groupes d'erreurs et la liste. Les compteurs serveur restent exacts.");
        public static string OptionParallel => T("Parallel count queries", "Requêtes de comptage en parallèle");
        public static string OptionParallelHelp => T("Aggregate queries sent at the same time when counting more than 50,000 jobs (1 to 8). Lower it if you hit service protection limits.",
                                                     "Requêtes d'agrégat envoyées en même temps pour compter plus de 50 000 jobs (1 à 8). Réduisez-le en cas de limites de protection du service.");
        public static string OptionBatchSize => T("Batch size of bulk actions", "Taille des lots des actions en masse");
        public static string OptionBatchSizeHelp => T("Number of jobs sent per ExecuteMultiple request (1 to 1,000). Lower it if you hit service protection limits.",
                                                      "Nombre de jobs envoyés par requête ExecuteMultiple (1 à 1 000). Réduisez-le en cas de limites de protection du service.");

        #endregion

        #region Export

        public static string Exporting => T("Exporting to Excel…", "Export Excel en cours…");
        public static string ExportFailed => T("The export failed", "L'export a échoué");
        public static string ExportDone(string path) => T($"Report saved:\r\n{path}\r\n\r\nOpen it now?", $"Rapport enregistré :\r\n{path}\r\n\r\nL'ouvrir maintenant ?");
        public static string SheetSummary => T("Summary", "Synthèse");
        public static string SheetTopFailing => T("Top 10 failing processes", "Top 10 processus en échec");
        public static string SheetErrorGroups => T("Error groups", "Groupes d'erreurs");
        public static string SheetJobs => "Jobs";
        public static string SheetProcesses => T("Processes", "Processus");
        public static string SheetTables => T("Tables", "Tables");
        public static string SheetTypes => "Types";
        public static string ColItem => T("Item", "Élément");
        public static string ColValue => T("Value", "Valeur");
        public static string ColProblems => T("Failed + suspended", "Échecs + suspendus");
        public static string ColTotal => "Total";
        public static string ColFailureRate => T("Failure rate", "Taux d'échec");
        public static string ColDistinctErrors => T("Distinct errors", "Erreurs distinctes");
        public static string ColTopError => T("Most frequent error", "Erreur la plus fréquente");
        public static string ColLastFailure => T("Last failure", "Dernier échec");
        public static string ColLogicalName => T("Logical name", "Nom logique");
        public static string SummaryOrganization => T("Organization", "Organisation");
        public static string SummaryAnalyzedOn => T("Analyzed on", "Analysé le");
        public static string SummaryPeriod => T("Period", "Période");
        public static string SummaryLoaded => T("Jobs loaded", "Jobs chargés");
        public static string SummaryTruncated => T("Limit", "Limite");
        public static string SummaryProblems => T("Failed or suspended jobs", "Jobs en échec ou suspendus");
        public static string SummaryGroups => T("Distinct error causes", "Causes d'erreur distinctes");
        public static string SummaryServerCounts => T("Server counts (every status)", "Compteurs serveur (tous statuts)");
        public static string Approximate => T("lower bounds", "minimums");
        public static string Exact => T("exact", "exacts");

        #endregion
    }
}
