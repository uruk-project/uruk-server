# uruk-server
Server of the Uruk project

# What is the Uruk project?
The Uruk project provide an audit trail solution, including authentication, non-repudiation and integrity.

## Key concepts
### Security Event Tokens structure
The Uruk project use the Security Event Tokens [RFC8417](https://tools.ietf.org/html/rfc8417) as well for structure and delivery.
This kind of token ensure authentication, non-repudiation and integrity of the record itself.

### WORM, Hash chains & Merkle trees
The audit trail is persisted into a [WORM-like](https://en.wikipedia.org/wiki/Write_once_read_many) database. 
The integrity is provided by a [hash chain](https://en.wikipedia.org/wiki/Hash_chain) of all the audit trail records. A [Merkle tree](https://en.wikipedia.org/wiki/Merkle_tree) allows to verify the integrity.

# Why the name of `Uruk`?
Uruk was an [ancient city of Sumer](https://en.wikipedia.org/wiki/Uruk), during the Uruk period. 
At this period, [bulla](https://en.wikipedia.org/wiki/Bulla_(seal)) were used for tamper-proofing commercial and legal affairs.
