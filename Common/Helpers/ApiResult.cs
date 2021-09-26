namespace Common.Helpers
{
    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }

        public ErrorResult Error { get; set; }

        public ApiResult(bool success, T data)
        {
            this.Success = success;
            this.Data = data;
        }

        public ApiResult(T data, ErrorResult error)
        {
            this.Success = false;
            this.Data = data;
            this.Error = error;
        }

        public ApiResult(bool success, T data, int errorCode, string errorMessage)
        {
            this.Success = success;
            this.Data = data;
            this.Error = new ErrorResult(errorCode, errorMessage);
        }
    }
}