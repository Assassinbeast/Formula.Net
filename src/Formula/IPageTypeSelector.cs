using System;
using Microsoft.AspNetCore.Http;

namespace Formula
{
    public interface IPageTypeSelector
    {
		Type Find(HttpContext context);
    }
}
