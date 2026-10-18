using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SystemJobsDoctor
{
    /// <summary>
    /// Point d'entrée MEF du plugin « System Jobs Doctor » pour XrmToolBox.
    /// </summary>
    [Export(typeof(IXrmToolBoxPlugin)),
        ExportMetadata("Name", "System Jobs Doctor"),
        ExportMetadata("Description", "Diagnose system jobs (asyncoperation): dashboard, errors grouped by cause, plug-in trace logs, bulk resume / cancel / postpone / delete, storage cleanup with a bulk deletion job, Excel report. English / French."),
        // "AH" icon of the author's tools, 32x32
        ExportMetadata("SmallImageBase64", PluginIcons.Small),
        // "AH" icon, 80x80
        ExportMetadata("BigImageBase64", PluginIcons.Big),
        // Same tile as Entity Fields Analyser
        ExportMetadata("BackgroundColor", "orange"),
        ExportMetadata("PrimaryFontColor", "white"),
        ExportMetadata("SecondaryFontColor", "Black")]
    public class SystemJobsDoctorPlugin : PluginBase, IPayPalPlugin
    {
        /// <summary>Donation through the XrmToolBox PayPal button (same account as Entity Fields Analyser).</summary>
        public string DonationDescription => "System Jobs Doctor";

        public string EmailAccount => "hamzamraoui11@gmail.com";

        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SystemJobsDoctorControl();
        }
    }
}
