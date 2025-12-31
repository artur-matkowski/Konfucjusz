window.hcaptchaInterop = {
    /**
     * Called by the global hCaptcha callback with the response token.
     * Stores the DotNetObjectReference and invokes OnHcaptchaToken on the component.
     */
    onSuccess: function (token) {
        if (window.hcaptchaCallbackRef) {
            window.hcaptchaCallbackRef.invokeMethodAsync('OnHcaptchaToken', token);
        }
    }
};

/**
 * Global callback configured on the hCaptcha widget.
 * This is required because hCaptcha expects a global JS function name.
 */
function onHcaptchaSuccess(token) {
    if (window.hcaptchaInterop) {
        window.hcaptchaInterop.onSuccess(token);
    }
}
