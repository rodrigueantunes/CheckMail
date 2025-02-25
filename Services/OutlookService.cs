using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Outlook = Microsoft.Office.Interop.Outlook;
using CheckMail.Models;
using Microsoft.Win32;
using System.Diagnostics;

namespace CheckMail.Services
{
    public static class OutlookService
    {
        /// <summary>
        /// Récupère les e-mails des X derniers jours.
        /// </summary>
        public static Dictionary<string, Dictionary<string, Dictionary<string, List<EmailItem>>>> GetCategorizedItems(int numberOfDays)
        {
            try
            {
                var categorizedItems = new Dictionary<string, Dictionary<string, Dictionary<string, List<EmailItem>>>>()
        {
            { "E-mails", new Dictionary<string, Dictionary<string, List<EmailItem>>>() },
            { "Notifications", new Dictionary<string, Dictionary<string, List<EmailItem>>>() },
            { "Tâches", new Dictionary<string, Dictionary<string, List<EmailItem>>>() },
            { "Réunions", new Dictionary<string, Dictionary<string, List<EmailItem>>>() }
        };

                DateTime startDate = DateTime.Now.AddDays(-numberOfDays);
                DateTime endDate = DateTime.Now;

                Outlook.Application outlookApp = new Outlook.Application();
                Outlook.NameSpace outlookNamespace = outlookApp.GetNamespace("MAPI");

                Outlook.MAPIFolder inbox = outlookNamespace.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);
                Outlook.MAPIFolder sent = outlookNamespace.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderSentMail);

                System.Diagnostics.Debug.WriteLine("🚀 Début de la récupération des e-mails...");

                // ⚡ Exécution simultanée pour optimiser la vitesse
                var taskInbox = Task.Run(() => ProcessFolderItemsRecursive(inbox, "Reçu", startDate, endDate, categorizedItems));
                var taskSent = Task.Run(() => ProcessFolderItems(sent.Items, "Envoyé", startDate, endDate, categorizedItems));

                Task.WaitAll(taskInbox, taskSent);

                System.Diagnostics.Debug.WriteLine("✅ Fin de la récupération des e-mails.");

                return categorizedItems;
            }
            catch (System.Exception ex)
            {
                throw new System.Exception("Erreur lors de la récupération des éléments : " + ex.Message);
            }
        }

        /// <summary>
        /// Fonction récursive pour analyser tous les sous-dossiers de la boîte de réception.
        /// </summary>
        private static void ProcessFolderItemsRecursive(Outlook.MAPIFolder folder, string type, DateTime startDate, DateTime endDate,
            Dictionary<string, Dictionary<string, Dictionary<string, List<EmailItem>>>> categorizedItems)
        {
            System.Diagnostics.Debug.WriteLine($"📂 Analyse du dossier : {folder.Name}");

            // 🔍 Si le dossier contient des e-mails, on les traite
            if (folder.Items.Count > 0)
            {
                ProcessFolderItems(folder.Items, type, startDate, endDate, categorizedItems);
            }

            // ⚡ Exécution en parallèle pour chaque sous-dossier
            Parallel.ForEach(folder.Folders.Cast<Outlook.MAPIFolder>(), subFolder =>
            {
                ProcessFolderItemsRecursive(subFolder, type, startDate, endDate, categorizedItems);
            });
        }

        private static void ProcessFolderItems(Outlook.Items items, string type, DateTime startDate, DateTime endDate,
     Dictionary<string, Dictionary<string, Dictionary<string, List<EmailItem>>>> categorizedItems)
        {
            string propertyName = (type == "Envoyé") ? "SentOn" : "ReceivedTime";

            // 🔥 Restrict() avec un format optimisé
            string filter = string.Format(
                CultureInfo.InvariantCulture,
                "[{0}] >= '{1}' AND [{0}] <= '{2}'",
                propertyName,
                startDate.Date.ToString("g", CultureInfo.InvariantCulture),
                endDate.Date.ToString("g", CultureInfo.InvariantCulture)
            );

            System.Diagnostics.Debug.WriteLine($"🔍 Filtrage rapide avec Restrict() pour {type} : {filter}");

            Outlook.Items filteredItems;
            try
            {
                filteredItems = items.Restrict(filter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Erreur Restrict() : {ex.Message}");
                filteredItems = items;
            }

            int count = 0;
            foreach (object item in filteredItems)
            {
                if (item is Outlook.MailItem mail)
                {
                    DateTime emailDate = (type == "Envoyé") ? mail.SentOn : mail.ReceivedTime;

                    // 🔥 Vérification stricte de la date pour éviter tout bug avec Restrict()
                    if (emailDate < startDate || emailDate > endDate)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Email ignoré (hors plage) : {mail.Subject} - {emailDate}");
                        continue;
                    }

                    string emailAddress = (type == "Envoyé") ? mail.To : (mail.SenderEmailAddress ?? "Inconnu");

                    AddToCategory("E-mails", type, emailAddress, mail.Subject, emailDate, categorizedItems);
                    count++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"✅ {count} e-mails trouvés après filtrage par date pour {type}");
        }


        private static void AddToCategory(string category, string type, string client, string subject, DateTime date,
            Dictionary<string, Dictionary<string, Dictionary<string, List<EmailItem>>>> categorizedItems)
        {
            if (!categorizedItems.TryGetValue(category, out var typeDict))
                categorizedItems[category] = typeDict = new Dictionary<string, Dictionary<string, List<EmailItem>>>();

            if (!typeDict.TryGetValue(type, out var clientDict))
                typeDict[type] = clientDict = new Dictionary<string, List<EmailItem>>();

            if (!clientDict.TryGetValue(client, out var emailList))
                clientDict[client] = emailList = new List<EmailItem>();

            emailList.Add(new EmailItem(client, type, subject, date));
        }
  
/// <summary>
/// Récupère le chemin d'installation de Outlook depuis le registre.
/// </summary>
        private static string GetOutlookInstallationPath()
        {
            string[] registryKeys =
            {
                @"SOFTWARE\Microsoft\Office\16.0\Outlook\InstallRoot",
                @"SOFTWARE\Microsoft\Office\15.0\Outlook\InstallRoot",
                @"SOFTWARE\WOW6432Node\Microsoft\Office\16.0\Outlook\InstallRoot",
                @"SOFTWARE\WOW6432Node\Microsoft\Office\15.0\Outlook\InstallRoot"
            };

            foreach (var key in registryKeys)
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(key))
                {
                    if (registryKey != null)
                    {
                        object path = registryKey.GetValue("Path");
                        if (path != null)
                        {
                            return Path.Combine(path.ToString(), "MSOUTL.OLB");
                        }
                    }
                }
            }
            return string.Empty;
        }
    }
}
