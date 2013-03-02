using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.Credentials
{
    // This shouldn't need to be thread safe if it is only accessed on the message pump thread.
    public class CredentialManager
    {
        public const int DefaultCapacity = 8;

        private CredentialSlot[] _slots;

        public CredentialManager()
        {
            // Slot 0 should always be empty.
            _slots = new CredentialSlot[DefaultCapacity + 1];
        }

        public void Resize(int newSize)
        {
            newSize++; // Slot 0 should always be empty.
            CredentialSlot[] newSlots = new CredentialSlot[newSize];
            Array.Copy(_slots, newSlots, Math.Min(_slots.Length, newSize));
            _slots = newSlots;
        }

        public bool TryGetCredential(int index, out CredentialSlot slot)
        {
            if (index <= 0 || index >= _slots.Length)
            {
                slot = null;
                return false;
            }
            slot = _slots[index];
            return slot != null;
        }

        public bool TrySetCredential(int index, CredentialSlot slot)
        {
            if (index <= 0 || index >= _slots.Length)
            {
                return false;
            }
            _slots[index] = slot;
            return true;
        }
    }
}
