using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Helpers
{
    public static class ArrayHelper
    { 
        public static T[,] Transpose<T>(this T[,] matrix)
        {
            var rows = matrix.GetLength(0);
            var columns = matrix.GetLength(1);

            var result = new T[columns, rows];

            for (var c = 0; c < columns; c++)
            {
                for (var r = 0; r < rows; r++)
                {
                    result[c, r] = matrix[r, c];
                }
            }

            return result;
        }
    }
}
