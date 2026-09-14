declare module 'toastr' {
  export type ToastrOptions = {
    closeButton?: boolean
    newestOnTop?: boolean
    progressBar?: boolean
    positionClass?: string
    preventDuplicates?: boolean
    timeOut?: number
    extendedTimeOut?: number
    tapToDismiss?: boolean
    escapeHtml?: boolean
  }

  type ToastMethod = (message: string, title?: string, optionsOverride?: ToastrOptions) => unknown

  const toastr: {
    options: ToastrOptions
    success: ToastMethod
    info: ToastMethod
    warning: ToastMethod
    error: ToastMethod
    clear: () => void
    remove: () => void
  }

  export default toastr
}
