using System;

namespace XpandTestExecutor.Module.BusinessObjects {
    public class DateTimeRange {
        private readonly DateTime _start;
        private readonly DateTime _end;

        public DateTimeRange(DateTime start, DateTime end) {
            _start = start;
            _end = end;
        }

        public DateTime End {
            get { return _end; }
        }

        public DateTime Start {
            get { return _start; }
        }

        public bool Intersects(DateTimeRange test) {
            if (InvalidRange(test))
                return false;

            if (NoActualDateRange(test))
                return false;

            if (OverlapWhenSameTime(test))
                return true;

            if (Start < test.Start) {
                if (End > test.Start && End < test.End)
                    return true;

                if (End > test.End)
                    return true;
            }
            else {
                if (test.End > Start && test.End < End)
                    return true;

                if (test.End > End)
                    return true;
            }

            return false;
        }

        private bool OverlapWhenSameTime(DateTimeRange test) {
            return Start == test.Start || End == test.End;
        }

        private bool NoActualDateRange(DateTimeRange test) {
            return Start == End || test.Start == test.End;
        }

        private bool InvalidRange(DateTimeRange test) {
            return Start > End || test.Start > test.End;
        }
    }
}