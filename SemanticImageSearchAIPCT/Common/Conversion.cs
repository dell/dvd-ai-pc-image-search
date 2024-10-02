using Microsoft.ML.OnnxRuntime.Tensors;

namespace SemanticImageSearchAIPCT.Common
{
    public static class Conversion
    {
        public static float[,,] To3DArray(Tensor<float> tensor, int dim1, int dim2, int dim3)
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

        public static float[,,,] To4DArray(Tensor<float> tensor, int dim1, int dim2, int dim3, int dim4)
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
            }

            return result;
        }

        public static float[] Flatten(float[,,,] array)
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
