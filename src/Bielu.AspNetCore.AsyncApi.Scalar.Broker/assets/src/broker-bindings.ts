import {
  deref,
  exampleFromSchema,
  extractSecuritySchemes,
  loadAsyncApiDocuments,
} from '@bielu/scalar-core'
import type { BrokerChannelModel, BrokerDocumentRef, BrokerProtocol, SecuritySchemeModel } from './types'

type AnyRecord = Record<string, any>

/** The protocols this console can drive, in the order they are offered. */
const SUPPORTED_PROTOCOLS: BrokerProtocol[] = ['kafka', 'mqtt', 'amqp']

/** A parsed document: its broker channels plus the security schemes its auth resolves against. */
export type BrokerDocumentModel = {
  name: string
  channels: BrokerChannelModel[]
  securitySchemes: Record<string, SecuritySchemeModel>
}

/**
 * The wire-level channel name the bridge should address.
 *
 * AsyncAPI 3 puts the transport name in the channel's `address`; the map key is only an id. The
 * protocol bindings may override it (Kafka's `topic`, AMQP's queue/exchange name), which is what a
 * broker actually routes on — so a binding wins over `address`, and `address` over the id.
 */
function channelAddress(id: string, channel: AnyRecord, protocol: BrokerProtocol, binding: AnyRecord): string {
  if (protocol === 'kafka' && typeof binding.topic === 'string') {
    return binding.topic
  }
  if (protocol === 'amqp') {
    const queue = binding.queue as AnyRecord | undefined
    if (queue && typeof queue.name === 'string') {
      return queue.name
    }
    const exchange = binding.exchange as AnyRecord | undefined
    if (exchange && typeof exchange.name === 'string') {
      return exchange.name
    }
  }
  return typeof channel.address === 'string' ? channel.address : id
}

/** The protocol a channel's bindings declare, if it is one this console supports. */
function channelProtocol(bindings: AnyRecord | undefined): BrokerProtocol | undefined {
  if (!bindings) {
    return undefined
  }
  return SUPPORTED_PROTOCOLS.find((protocol) => bindings[protocol] !== undefined)
}

/**
 * A prefilled publish body for a channel, from the payload schema of its first message.
 *
 * Best effort: a channel with no messages, or messages whose payloads are not JSON Schema, simply
 * gets no example and the user types the body themselves.
 */
function channelExample(doc: AnyRecord, channel: AnyRecord): string | undefined {
  const messages = deref(doc, channel.messages as AnyRecord | undefined)
  if (!messages) {
    return undefined
  }
  for (const message of Object.values(messages)) {
    const resolved = deref(doc, message as AnyRecord)
    const payload = deref(doc, resolved?.payload as AnyRecord | undefined)
    if (!payload) {
      continue
    }
    try {
      const example = exampleFromSchema(doc, payload)
      if (example !== undefined) {
        return JSON.stringify(example, null, 2)
      }
    } catch {
      // A schema we cannot turn into an example is not an error - fall through to the next message.
    }
  }
  return undefined
}

/** Extracts the broker channels a single parsed AsyncAPI document declares. */
export function parseBrokerChannels(documentName: string, doc: AnyRecord): BrokerChannelModel[] {
  const channels = (doc.channels ?? {}) as AnyRecord
  const models: BrokerChannelModel[] = []

  for (const [id, rawChannel] of Object.entries(channels)) {
    const channel = deref(doc, rawChannel as AnyRecord)
    if (!channel) {
      continue
    }
    const bindings = deref(doc, channel.bindings as AnyRecord | undefined)
    const protocol = channelProtocol(bindings)
    if (!protocol || !bindings) {
      continue
    }

    models.push({
      id,
      address: channelAddress(id, channel, protocol, (bindings[protocol] ?? {}) as AnyRecord),
      protocol,
      description: typeof channel.description === 'string' ? channel.description : undefined,
      documentName,
      example: channelExample(doc, channel),
    })
  }

  return models
}

/** Loads every configured document and extracts the broker channels each one declares. */
export async function loadBrokerDocuments(documents: BrokerDocumentRef[]): Promise<BrokerDocumentModel[]> {
  const loaded = await loadAsyncApiDocuments(documents)
  return loaded
    .map(({ name, doc }) => ({
      name,
      channels: parseBrokerChannels(name, doc),
      securitySchemes: extractSecuritySchemes(doc),
    }))
    // A document with no broker bindings has nothing for this console to show.
    .filter((model) => model.channels.length > 0)
}
