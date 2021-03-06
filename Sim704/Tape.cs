﻿using System;
using System.Collections.Generic;

namespace Sim704
{
    class Tape : IDisposable, I704dev
    {
        uint unit; /* Tape unit 1-10 */
        bool eof; /* end of file reached */
        bool wbin; /* currently writing a binary record */
        TapeFile f; /* for tape file access */
        ulong[] RRecord; /* read record */
        List<ulong> WRecord; /* write record */
        bool ReadActive; /* read is active */
        bool WriteActive; /* write is active */

        int PosInRecord; /* index in RRecord for next read word */

        void EndRW() /* finish current reading or writing operation */
        {
            if (ReadActive)
            {
                RRecord = null;
                ReadActive = false;
                if (Io704.Config.LogIO!=null)
                    Io704.LogIO.WriteLine("Tape {0} end read", unit);
            }
            if (WriteActive)
            {
                byte[] tr = new byte[WRecord.Count * 6];
                int i = 0;
                foreach (long w in WRecord)
                {
                    long wt = w;
                    for (int j = i + 5; j >= i; j--)
                    {
                        tr[j] = (byte)(wt & 0x3F);
                        wt >>= 6;
                    }
                    i += 6;
                }
                f.WriteRecord(wbin, tr);
                if (Io704.Config.LogIO!=null)
                    Io704.LogIO.WriteLine("Tape {0}: {1} record {2} with length {3} written",unit, wbin ? "binary" : "BCD", f.NumOfRecords(), WRecord.Count);
                WriteActive = false;
                WRecord.Clear();
            }
        }
        public Tape(uint u)
        {
            unit = u;
            f = null;
            ReadActive = false;
            WriteActive = false;

            WRecord = new List<ulong>();
        }
        public void MountTape(string file, bool rdonly) /* Mount Tape on unit */
        {
            if (f != null)
            {
                throw new InvalidOperationException(string.Format("tape on unit {0} already mounted", unit));
            }
            if (file != null)
                f = new TapeFile(file, rdonly);
            else
                f = null;
        }
        public void UnMountTape() /* Unmount Tape on unit */
        {
            EndRW();
            f.Dispose();
            f = null;
        }
        public void RT(bool binary) /* Read Select*/
        {
            EndRW();
            //ALU.MQ = (W36)0;
            if (f == null)
                eof = true;
            else
            {
                int r = f.ReadRecord(out bool rbinary, out byte[] mrecord);
                if (r == -1)
                {
                    if (Io704.Config.LogIO!=null)
                        Io704.LogIO.WriteLine("Tape {0} EOM", unit);
                }
                else if (r == 0)
                {
                    if (Io704.Config.LogIO!=null)
                        Io704.LogIO.WriteLine("Tape {0}: EOF read at record {1}", unit, f.NumOfRecords());
                }
                if (r < 1)
                    eof = true;
                else
                {                    
                    if (binary != rbinary)
                        Io704.tapecheck = true;
                    RRecord = new ulong[(mrecord.Length + 5) / 6];
                    if (Io704.Config.LogIO != null)
                        Io704.LogIO.WriteLine("Tape {0}: {1} record {2} with length {3} read", unit, rbinary ? "binary" : "BCD", f.NumOfRecords(), RRecord.Length);
                    for (int i = 0; i < mrecord.Length; i++)
                    {
                        RRecord[i / 6] <<= 6;
                        RRecord[i / 6] |= mrecord[i];
                    }
                    int remain = RRecord.Length * 6 - mrecord.Length;
                    for (int i = 0; i < remain; i++)
                        RRecord[mrecord.Length / 6] <<= 6;
                    eof = false;
                }
            }
            ReadActive = true;
            PosInRecord = 0;
        }
        public void WT(bool binary) /* Write Select*/
        {
            EndRW();
            wbin = binary;
            WriteActive = true;
            if (Io704.Config.LogIO!=null)
                Io704.LogIO.WriteLine("Tape {0} start write {1}", unit,binary?"binary":"BDC");
        }
        public int CPY(ref ulong w) /* Copy */
        {
            int ret = 0;
            if (ReadActive)
            {
                if (eof)
                {
                    if (Io704.Config.LogIO!=null)
                        Io704.LogIO.WriteLine("Tape {0} EOF", unit);
                    ret = 1;
                }
                else if (PosInRecord >= RRecord.Length)
                {
                    if (Io704.Config.LogIO!=null)
                        Io704.LogIO.WriteLine("Tape {0} EOR", unit);
                    ret = 2;
                }
                else
                {
                    w = RRecord[PosInRecord++];
                    ALU.MQ = (W36)w;
                    if (Io704.Config.LogIO!=null)
                        Io704.LogIO.WriteLine("Copy {0} from Tape {1}",  ALU.MQ,unit);
                }
            }
            else if (WriteActive)
            {
                ALU.MQ = (W36)w;
                if (Io704.Config.LogIO!=null)
                    Io704.LogIO.WriteLine("Copy {0} to Tape {1}", ALU.MQ, unit);
                WRecord.Add(w);
            }
            else
                throw new InvalidOperationException("CPY while device not selected");
            return ret;
        }
        public void BST() /* Backspace */
        {
            EndRW();
            if (Io704.Config.LogIO!=null)
                Io704.LogIO.WriteLine("Tape {0} Backspace", unit);
            f.BackSpace();
        }
        public void WEF() /* Write End of File */
        {
            EndRW();
            if (Io704.Config.LogIO!=null)
                Io704.LogIO.WriteLine("Tape {0} Write EOF", unit);
            f.WriteEOF();
        }
        public void REW() /* Rewind */
        {
            EndRW();
            if (Io704.Config.LogIO!=null)
                Io704.LogIO.WriteLine("Tape {0} rewind", unit);
            f.Rewind();
        }
        public void Disconnect() /* Disconnect from Device */
        {
            EndRW();
        }
        public void Dispose() /* IDisposable-Handling */
        {
            Dispose(true);
        }
        protected virtual void Dispose(bool disposing) /* IDisposable-Handling */
        {
            if (disposing)
            {
                // free managed resources  
                if (f != null)
                {
                    UnMountTape();
                }
            }
        }
    }
}
