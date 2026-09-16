#include <Security/Security.h>
#include <stdio.h>

_Static_assert(sizeof(Boolean) == 1, "Keychain Boolean width");
_Static_assert(sizeof(UInt32) == 4, "Keychain length width");
_Static_assert(sizeof(OSStatus) == 4, "Keychain status width");

/* Legacy items require the legacy process-local interaction guard. No item is read. */
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
int main(void) {
    OSStatus (*get)(Boolean *) = SecKeychainGetUserInteractionAllowed;
    OSStatus (*set)(Boolean) = SecKeychainSetUserInteractionAllowed;
    OSStatus (*find)(CFTypeRef, UInt32, const char *, UInt32, const char *, UInt32 *, void **, SecKeychainItemRef *) = SecKeychainFindGenericPassword;
    OSStatus (*release)(SecKeychainAttributeList *, void *) = SecKeychainItemFreeContent;
    if (!get || !set || !find || !release) return 1;
    puts("PASS Keychain SDK signatures and integer widths; no credential query performed");
    return 0;
}
#pragma clang diagnostic pop
