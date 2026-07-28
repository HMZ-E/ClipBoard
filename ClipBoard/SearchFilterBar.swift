import SwiftUI

struct SearchFilterBar: View {
    @Binding var searchText: String

    var body: some View {
        TextField("Search...", text: $searchText)
            .textFieldStyle(.roundedBorder)
    }
}
