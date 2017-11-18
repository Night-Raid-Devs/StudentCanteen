using System.Collections.Generic;
using BackendCommon;
using System.ServiceModel;

namespace BackendDatabase
{
    public interface IDatabase
    {
        void Initialize(ConnectionData data);

        #region Products

        string CreateProduct(string product);

        string UpdateProduct(string product);

        string GetProducts();

        string DeleteProduct(string product);

        string GetProduct(long productId);

        #endregion

        #region Orders

        string CreateOrder(string order);

        string GetOrders();

        string DeleteOrder(string order);

        string GetOrder(long orderId);

        #endregion

        #region Suppliers

        string CreateSupplier(string supplier);

        string UpdateSupplier(string supplier);

        string GetSuppliers();

        string DeleteSupplier(string supplier);

        string GetSupplier(long supplierId);

        #endregion
    }
}