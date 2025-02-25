using System;
using System.Collections.ObjectModel;
using System.IO; // Ajout de la référence pour Path

namespace CheckMail.Models
{
    public class EmailItem
    {
        public string Email { get; set; }
        public string Type { get; set; }
        public string Subject { get; set; }
        public DateTime Date { get; set; }

        // Ajout d'un constructeur pour éviter l'erreur CS1729
        public EmailItem(string email, string type, string subject, DateTime date)
        {
            Email = email;
            Type = type;
            Subject = subject;
            Date = date;
        }
    }

    public class EmailGroup
    {
        public string Name { get; set; }
        public ObservableCollection<EmailItem> Emails { get; set; } = new ObservableCollection<EmailItem>();
    }

    public class EmailCategory
    {
        public string Name { get; set; }
        public ObservableCollection<EmailGroup> SubCategories { get; set; } = new ObservableCollection<EmailGroup>();
    }
}