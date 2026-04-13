import Swal from 'sweetalert2';

export const showSuccess = (message: string, title: string = '¡Éxito!') => {
    return Swal.fire({
        icon: 'success',
        iconColor: '#107c10',
        title,
        text: message,
        confirmButtonColor: '#107c10',
    });
};

export const showError = (message: string, title: string = 'Error') => {
    return Swal.fire({
        icon: 'error',
        iconColor: '#d13438',
        title,
        text: message,
        confirmButtonColor: '#d13438',
    });
};

export const showWarning = (message: string, title: string = 'Atención') => {
    return Swal.fire({
        icon: 'warning',
        iconColor: '#f7630c',
        title,
        text: message,
        confirmButtonColor: '#f7630c',
    });
};

export const showInfo = (message: string, title: string = 'Información') => {
    return Swal.fire({
        icon: 'info',
        iconColor: '#0f6cbd',
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
