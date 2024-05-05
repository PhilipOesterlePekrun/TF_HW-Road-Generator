using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    public class StringUtil : MonoBehaviour // Functions taken from Youtube Playlist Utility
    {
        public static int[] checkForIn(string checkFor, string checkIn)
        {
            int[] returnArray = new int[20];
            for (int i = 0; i < returnArray.Length; i++)
            {
                returnArray[i] = -1;
            }
            string checkInTemp = checkIn;
            int arrayPos = 0;
            if (checkFor.Length <= checkIn.Length)
            {
                for (int i = 0; i <= checkIn.Length - checkFor.Length; i++)
                {
                    //checkInTemp = deleteInterval(checkInTemp, i, checkInTemp.Length);
                    for (int j = 0; j < checkFor.Length; j++)
                    {
                        if (checkFor[j] != checkIn[i + j])
                        {
                            break;
                        }
                        if (j == checkFor.Length - 1)
                        {
                            returnArray[arrayPos] = i;
                            arrayPos++;
                        }
                    }
                }
            }
            return returnArray;
        }
        public static string deleteInterval(string text, int from, int to) //can be composed easily; from==0 --> char 0 deleted, to>=length --> last char deleted; from==to --> one char at that pos deleted
        {
            string temp = "";
            //int j = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!(i >= from && i <= to))
                {
                    temp = temp + text[i];
                    //temp[j] = text[i];
                    //j++;
                }
            }
            return temp;
        }
    }
}