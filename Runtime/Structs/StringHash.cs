using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Diagnostics;

namespace CrowdMorph
{
   [Serializable]
   public struct StringHash : IEquatable<StringHash>
   {
      public uint Id;

      // ----------------------------------------------------------------------------------------
      // Constructors
      // ----------------------------------------------------------------------------------------

      public StringHash(string str)
      {
         Id = Hash(str);
         AddEntryToStringTable(Id, str);
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override int GetHashCode() => (int)Id;

      public override bool Equals(object other)
      {
         if (other == null || !(other is StringHash))
            return false;

         return Id == ((StringHash)other).Id;
      }

      public override string ToString()
      {
#if UNITY_EDITOR
         if (s_EditorStringTable.TryGetValue(Id, out var str))
            return str;
#endif
         return Id.ToString("X").PadLeft(8, '0');
      }

      // ----------------------------------------------------------------------------------------
      // Operators
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static public implicit operator StringHash(string str) => new StringHash(str);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static public implicit operator StringHash(uint id) => new StringHash { Id = id };

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static public bool operator ==(StringHash lhs, StringHash rhs) => lhs.Id == rhs.Id;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static public bool operator !=(StringHash lhs, StringHash rhs) => lhs.Id != rhs.Id;

      // ----------------------------------------------------------------------------------------
      // Methods (IEquatable)
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(StringHash other) => Id == other.Id;

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      unsafe static internal uint Hash(string str)
      {
         if (str == null)
            return 0U;

         var charArray = (str == string.Empty) ? new char[] { '\0' } : str.ToCharArray();

         fixed (void* charPtr = &charArray[0]) 
            return math.hash(charPtr, UnsafeUtility.SizeOf<char>() * charArray.Length);
      }

      [Conditional("UNITY_EDITOR")]
      private static void AddEntryToStringTable(uint id, string str)
      {
         s_EditorStringTable[id] = str;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      static Dictionary<uint, string> s_EditorStringTable = new Dictionary<uint, string>();
   }
}
