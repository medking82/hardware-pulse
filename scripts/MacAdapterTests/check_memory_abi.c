#include <mach/mach.h>
#include <stddef.h>
#include <stdio.h>

_Static_assert(sizeof(natural_t) == 4, "Mach counter width");
_Static_assert(sizeof(vm_size_t) == 8, "host_page_size width");
_Static_assert(sizeof(size_t) == 8, "sysctl size width");
_Static_assert(HOST_VM_INFO64 == 4, "Memory flavor");
_Static_assert((offsetof(vm_statistics64_data_t, total_uncompressed_pages_in_compressor) + sizeof(uint64_t)) / sizeof(integer_t) == 38, "Supported memory prefix");
_Static_assert(offsetof(vm_statistics64_data_t, wire_count) == 12, "Wired offset");
_Static_assert(offsetof(vm_statistics64_data_t, purgeable_count) == 88, "Purgeable offset");
_Static_assert(offsetof(vm_statistics64_data_t, compressor_page_count) == 128, "Compressor offset");
_Static_assert(offsetof(vm_statistics64_data_t, internal_page_count) == 140, "Internal offset");
int main(void) { puts("PASS macOS memory ABI against native SDK headers"); return 0; }
