using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Agent.Core.Data.Model
{
    public class Update
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string FileSize { get; set; }
        public string SupportURL { get; set; }
        public string Severity { get; set; }

        public string TopPatchId { get; set; }      // TopPatch issued id for an update.
        public string VendorId { get; set; }        // Gets the revision-independent identifier of an update. Vendor specific.

        public string KB { get; set; }              // a Windows Update specific KB#
        
        public string DateInstalled { get; set; }
        public string DatePublished { get; set; }

        public bool IsHidden { get; set; }
        public bool IsInstalled { get; set; }

        public bool Equals(Update update)
        {
            bool results = false;

            if ((this.TopPatchId != null) && (update.TopPatchId != null))
            {
                if (this.TopPatchId.Equals(update.TopPatchId))
                {
                    results = true;
                }
            }
            else if ((this.VendorId != null) && (update.VendorId != null))
            {
                if ((this.VendorId.Equals(update.VendorId)) && (this.Name.Equals(update.Name)))
                {
                    results = true;
                }
            }
            else
            {
                if (this.Name.Equals(update.Name))
                {
                    results = true;
                }
            }

            return results;
        }
    }
}
