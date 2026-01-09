using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Infrastructure.Odoo.FormTransport;

public interface IHpModelConvert<in TInterface, out TModel>
{
    public TModel Convert(TInterface model);
}