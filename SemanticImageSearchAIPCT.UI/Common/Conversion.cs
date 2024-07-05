using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticImageSearchAIPCT.UI.Common
{
    public class Conversion
    {
        public float[,,] To3DArray(Tensor<float> tensor, int dim1, int dim2, int dim3)
        {
            var array = new float[dim1, dim2, dim3];
            var data = tensor.ToArray();

            int index = 0;
            for (int i = 0; i < dim1; i++)
                for (int j = 0; j < dim2; j++)
                    for (int k = 0; k < dim3; k++)
                        array[i, j, k] = data[index++];

            return array;
        }

        public float[,,,] To4DArray(Tensor<float> tensor, int dim1, int dim2, int dim3, int dim4)
        {
            if (tensor.Length != dim1 * dim2 * dim3 * dim4)
            {
                throw new ArgumentException("The total number of elements does not match the expected dimensions.");
            }

            float[,,,] result = new float[dim1, dim2, dim3, dim4];
            var data = tensor.ToArray();

            int index = 0;
            for (int i = 0; i < dim1; i++)
            {
                for (int j = 0; j < dim2; j++)
                {
                    for (int k = 0; k < dim3; k++)
                    {
                        for (int l = 0; l < dim4; l++)
                        {
                            result[i, j, k, l] = data[index++];
                        }
                    }
                }
                //// Print the array after the first dimension is created
                //Debug.WriteLine($"After dimension {i + 1} is created:");
                //for (int j = 0; j < dim2; j++)
                //{
                //    for (int k = 0; k < dim3; k++)
                //    {
                //        for (int l = 0; l < dim4; l++)
                //        {
                //            Debug.Write($"{result[i, j, k, l]}, ");
                //        }
                //        Debug.WriteLine(""); // New line for better readability
                //    }
                //    Debug.WriteLine("-----"); // Separator for each sub-dimension
                //}
            }

            return result;
        }

        public float[] Flatten(float[,,,] array)
        {
            int length = array.GetLength(0) * array.GetLength(1) * array.GetLength(2) * array.GetLength(3);
            float[] flat = new float[length];
            int index = 0;
            for (int i = 0; i < array.GetLength(0); i++)
                for (int j = 0; j < array.GetLength(1); j++)
                    for (int k = 0; k < array.GetLength(2); k++)
                        for (int l = 0; l < array.GetLength(3); l++)
                            flat[index++] = array[i, j, k, l];
            return flat;
        }
    }
}
