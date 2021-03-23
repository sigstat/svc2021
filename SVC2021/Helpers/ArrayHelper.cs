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

        public static T[,] Cast<S,T>(this S[,] sourceArray) where S:T
        {
            var rows = sourceArray.GetLength(0);
            var columns = sourceArray.GetLength(1);

            var result = new T[rows, columns];
            for (var c = 0; c < columns; c++)
            {
                for (var r = 0; r < rows; r++)
                {
                    result[r, c] = (T)sourceArray[r, c];
                }
            }
            return result;
        }
    }
}
