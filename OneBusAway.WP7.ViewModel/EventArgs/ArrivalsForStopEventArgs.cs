﻿using System;
using System.Net;
using System.Collections.Generic;
using OneBusAway.WP7.ViewModel.DataStructures;

namespace OneBusAway.WP7.ViewModel.EventArgs
{
    public class ArrivalsForStopEventArgs : System.EventArgs
    {
        public Exception error { get; private set; }
        public List<ArrivalAndDeparture> arrivals;
        public Stop stop { get; private set; }

        public ArrivalsForStopEventArgs(Stop stop, List<ArrivalAndDeparture> arrivals, Exception error)
        {
            this.error = error;
            this.arrivals = arrivals;
            this.stop = stop;
        }
    }
}