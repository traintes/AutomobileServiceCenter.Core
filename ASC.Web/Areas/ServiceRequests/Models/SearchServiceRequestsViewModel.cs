using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.ServiceRequests.Models
{
    public class SearchServiceRequestsViewModel
    {
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Requested Date")]
        public DateTime? RequestedDate { get; set; }
    }
}
