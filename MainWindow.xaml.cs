using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CheckMail.Models;
using CheckMail.Services;

namespace CheckMail
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, Dictionary<string, Dictionary<string, List<EmailItem>>>> _categorizedEmails;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnLoadEmails_Click(object sender, RoutedEventArgs e)
        {
            BtnLoadEmails.IsEnabled = false;
            BtnLoadEmails.Content = "Chargement...";

            if (!int.TryParse(NumberOfDaysTextBox.Text, out int numberOfDays) || numberOfDays <= 0)
            {
                MessageBox.Show("Veuillez saisir un nombre de jours valide.");
                BtnLoadEmails.IsEnabled = true;
                BtnLoadEmails.Content = "Charger les e-mails";
                return;
            }

            try
            {
                var emails = await Task.Run(() => OutlookService.GetCategorizedItems(numberOfDays));

                Dispatcher.Invoke(() =>
                {
                    if (emails == null || emails.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Aucun e-mail trouvé.");
                        MessageBox.Show("Aucun e-mail trouvé.");
                        EmailsGrid.Items.Clear();
                        UpdateResultCount();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ Nombre d'e-mails trouvés : {emails.Count}");
                        _categorizedEmails = emails;
                        DisplayEmails();
                        Dispatcher.Invoke(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ Nombre d'e-mails trouvés : {emails.Count}");
                            _categorizedEmails = emails;
                            DisplayEmails();
                            PopulateDomainFilter(); // Ajout du filtrage par nom de domaine
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Erreur lors du chargement des e-mails : " + ex.Message));
            }

            BtnLoadEmails.Content = "Charger les e-mails";
            BtnLoadEmails.IsEnabled = true;
        }

        private void DomainFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si aucun e-mail n'est chargé, ne rien faire.
            if (_categorizedEmails == null || _categorizedEmails.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠ Aucun e-mail disponible pour filtrer par domaine.");
                return;
            }

            // Récupérer le domaine sélectionné (par défaut "Tous")
            string selectedDomain = "Tous";
            if (DomainFilterComboBox.SelectedItem is ComboBoxItem item)
            {
                selectedDomain = item.Content?.ToString() ?? "Tous";
            }

            System.Diagnostics.Debug.WriteLine($"📌 Filtrage par domaine : {selectedDomain}");
            DisplayEmails(selectedDomain);
        }


        private void UpdateResultCount()
        {
            int count = EmailsGrid.Items.Count;
            LblResultCount.Content = $"Nombre d'e-mails trouvés : {count}";
        }

        private void DisplayEmails(string selectedDomain = "Tous")
        {
            if (_categorizedEmails == null || _categorizedEmails.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠ Aucun e-mail chargé, affichage annulé.");
                EmailsGrid.Items.Clear();
                UpdateResultCount();
                return;
            }

            // Récupérer la plage de dates depuis les DatePickers (si non sélectionnées, utiliser des valeurs extrêmes)
            DateTime startDate = StartDatePicker.SelectedDate.HasValue ? StartDatePicker.SelectedDate.Value : DateTime.MinValue;
            DateTime endDate = EndDatePicker.SelectedDate.HasValue ? EndDatePicker.SelectedDate.Value : DateTime.MaxValue;


            string selectedType = ((ComboBoxItem)TypeFilterComboBox.SelectedItem)?.Content?.ToString() ?? "Tous";
            // Si "Tous" est sélectionné pour le domaine, on ne filtre pas par domaine
            selectedDomain = selectedDomain == "Tous" ? null : selectedDomain;

            EmailsGrid.Items.Clear();

            var filteredEmails = _categorizedEmails
                .SelectMany(cat => cat.Value ?? new Dictionary<string, Dictionary<string, List<EmailItem>>>())
                .SelectMany(type => type.Value ?? new Dictionary<string, List<EmailItem>>())
                .SelectMany(client => client.Value ?? new List<EmailItem>())
                .Where(email =>
                    email.Date >= startDate && email.Date <= endDate &&
                    (selectedType == "Tous" || email.Type == selectedType) &&
                    (selectedDomain == null ||
                     (!string.IsNullOrWhiteSpace(email.Email) &&
                      email.Email.Split('@').Last().Trim().Replace("'", "").Replace("(", "").Replace(")", "")
                          .Equals(selectedDomain, StringComparison.OrdinalIgnoreCase)))
                )
                .ToList();

            System.Diagnostics.Debug.WriteLine($"✅ {filteredEmails.Count} e-mails après filtrage.");

            foreach (var email in filteredEmails)
            {
                EmailsGrid.Items.Add(email);
            }

            UpdateResultCount();
        }


        // Méthode pour peupler la ComboBox des domaines à partir des e-mails chargés
        private void PopulateDomainFilter()
        {
            if (_categorizedEmails == null || _categorizedEmails.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠ Aucun e-mail à analyser pour les domaines.");
                return;
            }

            // Extraction de tous les domaines uniques en retirant les caractères ' ( )
            var domains = _categorizedEmails
                .SelectMany(cat => cat.Value ?? new Dictionary<string, Dictionary<string, List<EmailItem>>>())
                .SelectMany(type => type.Value ?? new Dictionary<string, List<EmailItem>>())
                .SelectMany(client => client.Value ?? new List<EmailItem>())
                .SelectMany(email =>
                {
                    if (string.IsNullOrWhiteSpace(email.Email))
                        return Enumerable.Empty<string>();

                    // Séparer les adresses multiples et nettoyer
                    return email.Email.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(addr => addr.Trim())
                        .Where(addr => addr.Contains("@"))
                        .Select(addr => addr.Split('@').Last().Trim()
                            .Replace("'", "").Replace("(", "").Replace(")", ""));
                })
                .Where(domain => !string.IsNullOrEmpty(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"✅ {domains.Count} domaines uniques trouvés.");

            DomainFilterComboBox.Items.Clear();
            DomainFilterComboBox.Items.Add(new ComboBoxItem { Content = "Tous", IsSelected = true });
            foreach (var domain in domains)
            {
                DomainFilterComboBox.Items.Add(new ComboBoxItem { Content = domain });
            }
        }




        private void BtnFilterDate_Click(object sender, RoutedEventArgs e)
        {
            // Vérifier si _categorizedEmails contient des e-mails
            if (_categorizedEmails == null || _categorizedEmails.Count == 0)
            {
                MessageBox.Show("Aucun e-mail à filtrer.");
                return;
            }

            // Vérifier que les DatePickers ont bien une date sélectionnée
            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Veuillez sélectionner une plage de dates valide.");
                return;
            }

            DateTime startDate = StartDatePicker.SelectedDate.Value;
            DateTime endDate = EndDatePicker.SelectedDate.Value;

            // Vérifier si la sélection de type est correcte
            if (TypeFilterComboBox.SelectedItem == null)
            {
                MessageBox.Show("Sélectionnez un type d'e-mail.");
                return;
            }

            string selectedType = ((ComboBoxItem)TypeFilterComboBox.SelectedItem)?.Content?.ToString() ?? "Tous";

            EmailsGrid.Items.Clear();

            // 🔥 Correction du filtrage en évitant les valeurs `null`
            var filteredEmails = _categorizedEmails
                .SelectMany(cat => cat.Value ?? new Dictionary<string, Dictionary<string, List<EmailItem>>>())
                .SelectMany(type => type.Value ?? new Dictionary<string, List<EmailItem>>())
                .Where(client => client.Value != null) // Vérifie que `client.Value` n'est pas null
                .SelectMany(client => client.Value)
                .Where(email => email.Date >= startDate && email.Date <= endDate &&
                                (selectedType == "Tous" || email.Type == selectedType))
                .ToList();

            foreach (var email in filteredEmails)
            {
                EmailsGrid.Items.Add(email);
            }

            UpdateResultCount();
        }


        private void TypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_categorizedEmails == null || _categorizedEmails.Count == 0)
            {
                // Empêcher l'exécution si aucun email n'est chargé
                return;
            }

            DisplayEmails();
        }



        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();
            if (_categorizedEmails == null || EmailsGrid == null) return;

            var filteredEmails = _categorizedEmails.SelectMany(cat => cat.Value)
                .SelectMany(type => type.Value)
                .SelectMany(client => client.Value)
                .Where(email => email.Subject.ToLower().Contains(searchText) || email.Email.ToLower().Contains(searchText))
                .ToList();

            EmailsGrid.Items.Clear();
            foreach (var email in filteredEmails)
            {
                EmailsGrid.Items.Add(email);
            }
        }
    }
}
