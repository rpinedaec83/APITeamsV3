import Swal from 'sweetalert2';

export const showSuccess = (message: string, title: string = '¡Éxito!') => {
    return Swal.fire({
        icon: 'success',
        title,
        text: message,
        confirmButtonColor: '#0f6cbd',
    });
};

export const showError = (message: string, title: string = 'Error') => {
    return Swal.fire({
        icon: 'error',
        title,
        text: message,
        confirmButtonColor: '#0f6cbd',
    });
};

export const showWarning = (message: string, title: string = 'Atención') => {
    return Swal.fire({
        icon: 'warning',
        title,
        text: message,
        confirmButtonColor: '#0f6cbd',
    });
};

export const showInfo = (message: string, title: string = 'Información') => {
    return Swal.fire({
        icon: 'info',
        title,
        text: message,
        confirmButtonColor: '#0f6cbd',
    });
};

export const showConfirm = (message: string, title: string = '¿Estás seguro?') => {
    return Swal.fire({
        icon: 'question',
        title,
        text: message,
        showCancelButton: true,
        confirmButtonColor: '#0f6cbd',
        cancelButtonColor: '#d13438',
        confirmButtonText: 'Sí, continuar',
        cancelButtonText: 'Cancelar',
    });
};
