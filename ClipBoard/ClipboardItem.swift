import SwiftUI

// MARK: - Data Model
struct ClipboardItem: Identifiable, Equatable, Codable {
    let id: UUID
    let content: ClipboardContent
    let createdAt: Date

    init(id: UUID = UUID(), content: ClipboardContent, createdAt: Date = Date()) {
        self.id = id
        self.content = content
        self.createdAt = createdAt
    }

    static func == (lhs: ClipboardItem, rhs: ClipboardItem) -> Bool {
        lhs.id == rhs.id
    }
}

enum ClipboardContent: Equatable, Codable {
    case text(String)
    case image(Data)

    enum CodingKeys: String, CodingKey {
        case type, value
    }

    enum ContentType: String, Codable {
        case text, image
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let type = try container.decode(ContentType.self, forKey: .type)

        switch type {
        case .text:
            let value = try container.decode(String.self, forKey: .value)
            self = .text(value)
        case .image:
            let data = try container.decode(Data.self, forKey: .value)
            self = .image(data)
        }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)

        switch self {
        case .text(let text):
            try container.encode(ContentType.text, forKey: .type)
            try container.encode(text, forKey: .value)
        case .image(let data):
            try container.encode(ContentType.image, forKey: .type)
            try container.encode(data, forKey: .value)
        }
    }
}
