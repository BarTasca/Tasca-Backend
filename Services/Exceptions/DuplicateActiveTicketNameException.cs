using System;

namespace BarTasca.Services.Exceptions
{
    public class DuplicateActiveTicketNameException : Exception
    {
        public DuplicateActiveTicketNameException()
            : base("Este nombre ya está activo actualmente. Ya hay una persona en la cola esperando o avisada con ese nombre.")
        {
        }
    }
}
