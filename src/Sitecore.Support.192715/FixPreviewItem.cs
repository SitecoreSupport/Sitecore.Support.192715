using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Pipelines.HasPresentation;
using Sitecore.Publishing;
using Sitecore.Shell.DeviceSimulation;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Specialized;
using Sitecore.Web.Authentication;
using Sitecore.Security.Authentication;
using Sitecore.Security.Accounts;

namespace Sitecore.Support.Shell.Framework.Commands
{
    /// <summary>
    /// Represents the Preview Item command.
    /// </summary>
    [Serializable]
    public class FixPreviewItem : Sitecore.Shell.Framework.Commands.PreviewItem
    {
        /// <summary>
        /// Runs the specified args.
        /// </summary>
        /// <param name="args">The arguments.</param>
        protected new void Run(ClientPipelineArgs args)
        {
            Item item = Database.GetItem(ItemUri.Parse(args.Parameters["uri"]));
            if (item == null)
            {
                SheerResponse.Alert("Item not found.", new string[0]);
                return;
            }
            string value = item.ID.ToString();
            if (args.IsPostBack)
            {
                if (args.Result != "yes")
                {
                    return;
                }
                Item item2 = Context.ContentDatabase.GetItem(LinkManager.GetPreviewSiteContext(item).StartPath);
                if (item2 == null)
                {
                    SheerResponse.Alert("Start item not found.", new string[0]);
                    return;
                }
                value = item2.ID.ToString();
            }
            else if (!HasPresentationPipeline.Run(item))
            {
                SheerResponse.Confirm("The current item cannot be previewed because it has no layout for the current device.\n\nDo you want to preview the start Web page instead?");
                args.WaitForPostBack();
                return;
            }
            SheerResponse.CheckModified(false);
            SiteContext previewSiteContext = LinkManager.GetPreviewSiteContext(item);
            if (previewSiteContext == null)
            {
                SheerResponse.Alert(Translate.Text("Site \"{0}\" not found", new object[]
                {
                    Settings.Preview.DefaultSite
                }), new string[0]);
                return;
            }
            string cookieKey = previewSiteContext.GetCookieKey("sc_date");
            WebUtil.SetCookieValue(cookieKey, string.Empty);

            // Fix bug xxx : Backup and restore virtual user when preview an item
            if (Context.User.Profile.ProfileUser.RuntimeSettings.IsVirtual)
            {
                BackupAndRestoreVirtualUser(previewSiteContext);
            }
            else
            {
                PreviewManager.StoreShellUser(Settings.Preview.AsAnonymous);
            }            

            UrlString urlString = new UrlString("/");
            urlString["sc_itemid"] = value;
            urlString["sc_mode"] = "preview";
            urlString["sc_lang"] = item.Language.ToString();
            urlString["sc_site"] = previewSiteContext.Name;
            DeviceSimulationUtil.DeactivateSimulators();
            if (UIUtil.IsChrome())
            {
                SheerResponse.Eval("setTimeout(function () { window.open('" + urlString + "', '_blank');}, 0);");
                return;
            }
            SheerResponse.Eval("window.open('" + urlString + "', '_blank');");
        }

        // Fix bug xxx: Backup and Restore virtual user
        private void BackupAndRestoreVirtualUser(SiteContext previewSiteContext)
        {
            var virtualUser = Context.User;
            string userTicketCookieKey = string.Empty;
            string userTicketCookieValue = string.Empty;
            userTicketCookieKey = previewSiteContext.GetCookieKey(TicketManager.CookieName);
            userTicketCookieValue = WebUtil.GetCookieValue(userTicketCookieKey);

            PreviewManager.StoreShellUser(Settings.Preview.AsAnonymous);

            AuthenticationManager.LoginVirtualUser(virtualUser);
            WebUtil.SetCookieValue(userTicketCookieKey, userTicketCookieValue);
        }
    }
}
