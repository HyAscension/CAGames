using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CanadaGames.Models
{
    public class Coach : Auditable
    {
        public int ID { get; set; }

        [Display(Name = "Coach")]
        [DisplayFormat(NullDisplayText = "None")]
        public string FullName => FirstName + (string.IsNullOrEmpty(MiddleName) ? " " : (" " + (char?)MiddleName[0] + ". ").ToUpper()) + LastName;

        [Display(Name = "First Name")]
        [DisplayFormat(NullDisplayText = "None")]
        public string FirstNameI => FirstName + (string.IsNullOrEmpty(MiddleName) ? " " : (" " + (char?)MiddleName[0] + ". ").ToUpper());

        [Display(Name = "Coach")]
        [DisplayFormat(NullDisplayText = "None")]
        public string FormalName => LastName + ", " + FirstName + (string.IsNullOrEmpty(MiddleName) ? "" : (" " + (char?)MiddleName[0] + ".").ToUpper());

        [Display(Name = "First Name")]
        [Required(ErrorMessage = "You cannot leave the first name blank.")]
        [StringLength(50, ErrorMessage = "First name cannot be more than 50 characters long.")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        [StringLength(50, ErrorMessage = "Middle name cannot be more than 50 characters long.")]
        public string MiddleName { get; set; }

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "You cannot leave the last name blank.")]
        [StringLength(100, ErrorMessage = "Last name cannot be more than 100 characters long.")]
        public string LastName { get; set; }

        public ICollection<Athlete> Athletes { get; set; }

        public Coach()
        {
            Athletes = new HashSet<Athlete>();
        }

    }
}
