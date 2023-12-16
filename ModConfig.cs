using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InviteFriend
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;

        public bool EnableVisitInside { get; set; } = true;
        public float InviteComeTime { get; set; } = 1000;
        public float InviteLeaveTime { get; set; } = 2000;
        public int DialogueTime { get; set; } = 2;




    }
}
