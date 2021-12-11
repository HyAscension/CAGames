using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CanadaGames.ViewModels
{
    public class PlacementReport
    {
        public int ID { get; set; }

        [Display(Name = "Athlete")]
        public string FullName
        {
            get
            {
                return FirstName
                    + (string.IsNullOrEmpty(MiddleName) ? " " :
                        (" " + (char?)MiddleName[0] + ". ").ToUpper())
                    + LastName;
            }
        }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        [Display(Name = "Contingent Code")]
        public string ContingentCode { get; set; }

        [Display(Name = "Average Placement")]
        public double AvgPlace { get; set; }

        [Display(Name = "Maximum Placement")]
        public int MaxPlace { get; set; }

        [Display(Name = "Minimum Placement")]
        public int MinPlace { get; set; }

        [Display(Name = "Number Of Events")]
        public int NumberOfEvents { get; set; }

        public string MediaInfo { get; set; }

    }
}
